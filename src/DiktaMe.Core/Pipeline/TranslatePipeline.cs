namespace DiktaMe.Core.Pipeline;

using System.Diagnostics;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.STT;
using Serilog;

/// <summary>
/// Orchestrates the Translate flow:
/// Record → STT (auto-detect language) → LLM (translate) → Inject.
/// Port of V1's translate path in <c>process_recording</c> (pipelines.py).
/// </summary>
public sealed class TranslatePipeline
{
    private readonly ISTTProvider _stt;
    private readonly ILLMProvider _llm;
    private readonly TextInjector _injector;

    /// <summary>Raised when the pipeline transitions to a new stage.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Raised when the pipeline completes (success or failure).</summary>
    public event EventHandler<PipelineResult>? Completed;

    public TranslatePipeline(ISTTProvider stt, ILLMProvider llm, TextInjector injector)
    {
        _stt = stt;
        _llm = llm;
        _injector = injector;
    }

    /// <summary>
    /// Runs the Translate pipeline for the supplied audio file.
    /// Never throws — all exceptions are caught and returned as a failed <see cref="PipelineResult"/>.
    /// </summary>
    public async Task<PipelineResult> RunAsync(
        string audioFilePath,
        TranslateOptions options,
        CancellationToken cancellationToken = default)
    {
        const string Mode = "translate";
        var total = Stopwatch.StartNew();

        try
        {
            // ── Stage 1: Transcribe (often with auto language detection) ──
            SetState(PipelineState.Transcribing);
            Log.Information("TranslatePipeline: transcribing (language={Lang})", options.Language);

            var sttSw = Stopwatch.StartNew();
            TranscriptionResult sttResult = await _stt
                .TranscribeAsync(audioFilePath, options.Language, cancellationToken)
                .ConfigureAwait(false);
            sttSw.Stop();

            if (!sttResult.IsSuccess)
            {
                Log.Information("TranslatePipeline: empty transcription — aborting");
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "Empty transcription");
                Completed?.Invoke(this, empty);
                return empty;
            }

            string rawText = sttResult.Text;
            Log.Information("TranslatePipeline: transcribed {Chars} chars (detected lang: {Lang})",
                rawText.Length, sttResult.DetectedLanguage ?? "unknown");

            // ── Stage 2: Translate via LLM ────────────────────────────────
            SetState(PipelineState.Processing);
            Log.Information("TranslatePipeline: translating via {Provider}", _llm.ProviderName);

            var llmSw = Stopwatch.StartNew();
            LlmResult llmResult = await _llm
                .ProcessAsync(rawText, options.SystemPrompt, Mode, cancellationToken)
                .ConfigureAwait(false);
            llmSw.Stop();

            if (!llmResult.IsSuccess)
            {
                Log.Warning("TranslatePipeline: LLM returned empty — injecting raw transcript");
                // V1 behaviour: fall back to raw text
            }

            string finalText = llmResult.IsSuccess ? llmResult.Text : rawText;

            // ── Stage 3: Inject ───────────────────────────────────────────
            SetState(PipelineState.Injecting);
            Log.Information("TranslatePipeline: injecting {Chars} chars", finalText.Length);

            var injSw = Stopwatch.StartNew();
            _injector.InjectText(
                finalText,
                options.Injection.TrailingSpace,
                options.Injection.AdditionalKey);
            injSw.Stop();

            total.Stop();
            Log.Information(
                "TranslatePipeline: complete — total={Total}ms stt={Stt}ms llm={Llm}ms inj={Inj}ms",
                total.ElapsedMilliseconds, sttSw.ElapsedMilliseconds,
                llmSw.ElapsedMilliseconds, injSw.ElapsedMilliseconds);

            SetState(PipelineState.Idle);

            var result = new PipelineResult
            {
                Text = finalText,
                RawTranscript = rawText,
                Mode = Mode,
                IsSuccess = true,
                TranscriptionMs = sttSw.ElapsedMilliseconds,
                ProcessingMs = llmSw.ElapsedMilliseconds,
                InjectionMs = injSw.ElapsedMilliseconds,
                TotalMs = total.ElapsedMilliseconds,
                SttProvider = sttResult.Provider,
                LlmProvider = llmResult.Provider,
            };
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("TranslatePipeline: cancelled");
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TranslatePipeline: unhandled error");
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
        }
    }

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);
}
