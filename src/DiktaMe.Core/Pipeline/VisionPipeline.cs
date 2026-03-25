using System.Diagnostics;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.STT;
using DiktaMe.Core.Vision;
using Serilog;

namespace DiktaMe.Core.Pipeline;

/// <summary>
/// Orchestrates the vision flow: screenshot → optional voice query STT → multimodal LLM → text injection.
/// </summary>
public sealed class VisionPipeline
{
    private const string Mode = "vision";

    private readonly ILLMProvider _llm;
    private readonly TextInjector _injector;
    private readonly ISTTProvider? _stt;
    private readonly PipelineEventBus? _eventBus;

    public event EventHandler<PipelineState>? StateChanged;
    public event EventHandler<PipelineResult>? Completed;

    public VisionPipeline(
        ILLMProvider llm,
        TextInjector injector,
        ISTTProvider? stt = null,
        PipelineEventBus? eventBus = null)
    {
        _llm = llm;
        _injector = injector;
        _stt = stt;
        _eventBus = eventBus;
    }

    public async Task<PipelineResult> RunAsync(
        byte[] screenshotData,
        string mimeType,
        string? audioFilePath,
        VisionOptions options,
        CancellationToken cancellationToken = default)
    {
        var total = Stopwatch.StartNew();
        try
        {
            // Stage 1: Optional voice query transcription
            string queryText = options.DefaultQuery;
            long transcriptionMs = 0;
            string? sttProvider = null;

            if (audioFilePath is not null && _stt is not null)
            {
                SetState(PipelineState.Transcribing);
                var sttSw = Stopwatch.StartNew();
                var sttResult = await _stt.TranscribeAsync(audioFilePath, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                sttSw.Stop();
                transcriptionMs = sttSw.ElapsedMilliseconds;
                sttProvider = sttResult.Provider;

                if (!string.IsNullOrWhiteSpace(sttResult.Text))
                {
                    queryText = sttResult.Text;
                    Log.Information("VisionPipeline: voice query = '{Query}'", queryText);
                }
            }

            // Stage 2: Multimodal LLM processing
            SetState(PipelineState.Processing);

            // Apply BeforeLlm hooks
            var effectivePrompt = options.SystemPrompt;
            if (_eventBus is not null)
            {
                var ctx = new BeforeLlmContext { UserText = queryText, SystemPrompt = effectivePrompt, Mode = Mode };
                _eventBus.PublishBeforeLlm(ctx);
                if (ctx.AdditionalSystemContext.Length > 0)
                    effectivePrompt = $"{effectivePrompt}\n\n{ctx.AdditionalSystemContext}";
            }

            Log.Information("VisionPipeline: sending screenshot + query to LLM ({Provider})", _llm.ProviderName);
            var llmSw = Stopwatch.StartNew();
            LlmResult llmResult = await _llm
                .ProcessWithImageAsync(screenshotData, mimeType, queryText, effectivePrompt, Mode, cancellationToken)
                .ConfigureAwait(false);
            llmSw.Stop();

            if (!llmResult.IsSuccess || string.IsNullOrWhiteSpace(llmResult.Text))
            {
                var fail = PipelineResult.Failure(Mode, llmResult.Text ?? "Vision LLM returned empty response");
                total.Stop();
                Completed?.Invoke(this, fail);
                return fail;
            }

            string responseText = llmResult.Text;

            // Stage 3: Output (inject, clipboard, or toast-only)
            long injectionMs = 0;
            bool shouldInject = options.OutputMode is VisionOutputMode.Inject or VisionOutputMode.ToastInject;
            if (shouldInject)
            {
                SetState(PipelineState.Injecting);
                var injSw = Stopwatch.StartNew();
                _injector.InjectText(responseText);
                injSw.Stop();
                injectionMs = injSw.ElapsedMilliseconds;
            }

            total.Stop();
            var result = new PipelineResult
            {
                Text = responseText,
                Mode = Mode,
                IsSuccess = true,
                TranscriptionMs = transcriptionMs,
                ProcessingMs = llmSw.ElapsedMilliseconds,
                InjectionMs = injectionMs,
                TotalMs = total.ElapsedMilliseconds,
                SttProvider = sttProvider,
                LlmProvider = llmResult.Provider,
                InputTokens = llmResult.InputTokens,
                OutputTokens = llmResult.OutputTokens,
                TokensPerSec = llmResult.TokensPerSec,
            };

            SetState(PipelineState.Idle);
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            total.Stop();
            var cancelled = PipelineResult.Failure(Mode, "Vision pipeline cancelled");
            Completed?.Invoke(this, cancelled);
            return cancelled;
        }
        catch (NotSupportedException ex)
        {
            total.Stop();
            Log.Warning(ex, "VisionPipeline: provider does not support vision");
            var notSupported = PipelineResult.Failure(Mode,
                $"Selected LLM provider does not support vision/image analysis. {ex.Message}");
            Completed?.Invoke(this, notSupported);
            return notSupported;
        }
        catch (Exception ex)
        {
            total.Stop();
            Log.Error(ex, "VisionPipeline: unexpected error");
            var error = PipelineResult.Failure(Mode, $"Vision pipeline error: {ex.Message}");
            SetState(PipelineState.Error);
            Completed?.Invoke(this, error);
            return error;
        }
    }

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);
}
