
using System.Diagnostics;
using DiktaMe.Core.Config;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.STT;
using Serilog;

namespace DiktaMe.Core.Pipeline;
/// <summary>
/// Orchestrates the core dictation flow: STT → optional LLM cleanup → text injection.
/// The audio file is produced externally (by <c>AudioRecorder</c>) and passed in.
/// Port of V1's <c>PipelineExecutor.process_recording</c> in pipelines.py.
/// </summary>
public sealed class DictationPipeline
{
    private readonly ISTTProvider _stt;
    private readonly ILLMProvider? _llm;
    private readonly TextInjector _injector;
    private readonly SnippetManager? _snippets;

    // ── Events ────────────────────────────────────────────────────────────

    /// <summary>Raised when the pipeline moves to a new stage.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Raised when the pipeline completes (success or failure).</summary>
    public event EventHandler<PipelineResult>? Completed;

    /// <param name="stt">Provider (or router) to use for transcription.</param>
    /// <param name="llm">
    /// Optional provider (or router) for LLM cleanup.
    /// When null the pipeline always operates in raw mode.
    /// </param>
    /// <param name="injector">Text injector for writing results to the active window.</param>
    public DictationPipeline(
        ISTTProvider stt,
        ILLMProvider? llm,
        TextInjector injector,
        SnippetManager? snippets = null)
    {
        _stt = stt;
        _llm = llm;
        _injector = injector;
        _snippets = snippets;
    }

    /// <summary>
    /// Runs the dictation pipeline for the supplied audio file.
    /// Never throws — all exceptions are caught and returned as a failed <see cref="PipelineResult"/>.
    /// </summary>
    /// <param name="audioFilePath">Path to the 16kHz mono WAV file from the recorder.</param>
    /// <param name="options">Dictation options (prompt, raw mode, injection settings).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task<PipelineResult> RunAsync(
        string audioFilePath,
        DictationOptions options,
        CancellationToken cancellationToken = default)
    {
        const string Mode = "dictate";
        var total = Stopwatch.StartNew();

        try
        {
            // ── Stage 1: Transcribe ───────────────────────────────────────
            SetState(PipelineState.Transcribing);
            Log.Information("DictationPipeline: transcribing '{File}'", audioFilePath);

            var sttSw = Stopwatch.StartNew();
            TranscriptionResult sttResult = await _stt
                .TranscribeAsync(audioFilePath, options.Language, cancellationToken)
                .ConfigureAwait(false);
            sttSw.Stop();

            if (!sttResult.IsSuccess)
            {
                return EmitEmpty(Mode, options, sttResult, sttSw.ElapsedMilliseconds, total.ElapsedMilliseconds);
            }

            string rawText = sttResult.Text;
            Log.Information("DictationPipeline: transcribed {Chars} chars via {Provider}",
                rawText.Length, sttResult.Provider);

            // ── Stage 2: LLM cleanup (optional) ──────────────────────────
            SetState(PipelineState.Processing);

            string finalText = rawText;
            long processingMs = 0;
            string? llmProvider = null;
            string? warningMessage = null;
            double? tokensPerSec = null;

            bool useLlm = !options.RawMode
                && _llm is not null
                && !string.IsNullOrWhiteSpace(options.SystemPrompt);

            if (useLlm)
            {
                try
                {
                    Log.Information("DictationPipeline: sending to LLM ({Provider})", _llm!.ProviderName);
                    var llmSw = Stopwatch.StartNew();
                    LlmResult llmResult = await _llm!
                        .ProcessWithModelAsync(rawText, options.SystemPrompt!, options.ModelName, Mode, cancellationToken)
                        .ConfigureAwait(false);
                    llmSw.Stop();
                    processingMs = llmSw.ElapsedMilliseconds;
                    llmProvider = llmResult.Provider;
                    tokensPerSec = llmResult.TokensPerSec;

                    if (llmResult.IsSuccess)
                    {
                        finalText = llmResult.Text;
                        Log.Information("DictationPipeline: LLM produced {Chars} chars", finalText.Length);
                    }
                    else
                    {
                        Log.Warning("DictationPipeline: LLM returned empty — falling back to raw transcript");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // let outer catch handle cancellation
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "DictationPipeline: LLM failed — falling back to raw transcript");
                    warningMessage = "LLM unavailable — raw transcription injected";
                }
            }
            else
            {
                Log.Debug("DictationPipeline: raw mode — skipping LLM");
            }

            // ── Stage 3: Snippet expansion (post-LLM, pre-inject) ────────
            if (_snippets is not null)
            {
                finalText = _snippets.ExpandSnippets(finalText);
            }

            // ── Stage 4: Inject ───────────────────────────────────────────
            SetState(PipelineState.Injecting);
            Log.Information("DictationPipeline: injecting {Chars} chars", finalText.Length);

            var injSw = Stopwatch.StartNew();
            _injector.InjectText(
                finalText,
                options.Injection.TrailingSpace,
                options.Injection.AdditionalKey);
            injSw.Stop();

            total.Stop();

            Log.Information(
                "DictationPipeline: complete — total={Total}ms stt={Stt}ms llm={Llm}ms inj={Inj}ms words={Words}",
                total.ElapsedMilliseconds, sttSw.ElapsedMilliseconds,
                processingMs, injSw.ElapsedMilliseconds,
                finalText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

            SetState(PipelineState.Idle);

            var result = new PipelineResult
            {
                Text = finalText,
                RawTranscript = rawText,
                Mode = Mode,
                IsSuccess = true,
                WarningMessage = warningMessage,
                RecordingMs = options.RecordingDurationMs,
                TranscriptionMs = sttSw.ElapsedMilliseconds,
                ProcessingMs = processingMs,
                InjectionMs = injSw.ElapsedMilliseconds,
                TotalMs = total.ElapsedMilliseconds,
                SttProvider = sttResult.Provider,
                LlmProvider = llmProvider,
                AudioDurationSec = sttResult.AudioDurationSec,
                TokensPerSec = tokensPerSec,
            };
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("DictationPipeline: cancelled");
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DictationPipeline: unhandled error");
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
        }
    }

    private PipelineResult EmitEmpty(
        string mode, DictationOptions options, TranscriptionResult sttResult,
        long transcriptionMs, long totalMs)
    {
        Log.Information("DictationPipeline: empty transcription — aborting");
        SetState(PipelineState.Idle);
        var result = new PipelineResult
        {
            Text = string.Empty,
            RawTranscript = string.Empty,
            Mode = mode,
            IsSuccess = true,
            RecordingMs = options.RecordingDurationMs,
            TranscriptionMs = transcriptionMs,
            TotalMs = totalMs,
            SttProvider = sttResult.Provider,
            AudioDurationSec = sttResult.AudioDurationSec,
        };
        Completed?.Invoke(this, result);
        return result;
    }

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);
}
