
using System.Diagnostics;
using DiktaMe.Core.LLM;
using DiktaMe.Core.STT;
using Serilog;

namespace DiktaMe.Core.Pipeline;
/// <summary>
/// Orchestrates the Ask flow: record audio → transcribe → LLM Q&amp;A → return answer.
/// The answer is returned in the <see cref="PipelineResult"/> for the caller to display
/// (notification, window, or clipboard) — it is NOT injected.
/// Port of V1's <c>PipelineExecutor.process_ask_recording</c> in pipelines.py.
/// </summary>
public sealed class AskPipeline
{
    private readonly ISTTProvider _stt;
    private readonly ILLMProvider _llm;

    /// <summary>Raised when the pipeline transitions to a new stage.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Raised when the pipeline completes (success or failure).</summary>
    public event EventHandler<PipelineResult>? Completed;

    public AskPipeline(ISTTProvider stt, ILLMProvider llm)
    {
        _stt = stt;
        _llm = llm;
    }

    /// <summary>
    /// Runs the Ask pipeline for the supplied audio file.
    /// Never throws — all exceptions are caught and returned as a failed <see cref="PipelineResult"/>.
    /// </summary>
    public async Task<PipelineResult> RunAsync(
        string audioFilePath,
        AskOptions options,
        CancellationToken cancellationToken = default)
    {
        const string Mode = "ask";
        var total = Stopwatch.StartNew();

        try
        {
            // ── Stage 1: Transcribe ───────────────────────────────────────
            SetState(PipelineState.Transcribing);
            Log.Information("AskPipeline: transcribing question");

            var sttSw = Stopwatch.StartNew();
            TranscriptionResult sttResult = await _stt
                .TranscribeAsync(audioFilePath, options.Language, cancellationToken)
                .ConfigureAwait(false);
            sttSw.Stop();

            if (!sttResult.IsSuccess)
            {
                Log.Information("AskPipeline: empty transcription — aborting");
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "No question detected");
                Completed?.Invoke(this, empty);
                return empty;
            }

            string question = sttResult.Text;
            Log.Information("AskPipeline: question = '{Question}'", question);

            // ── Stage 2: Ask the LLM ──────────────────────────────────────
            SetState(PipelineState.Processing);
            Log.Information("AskPipeline: sending to LLM ({Provider})", _llm.ProviderName);

            var llmSw = Stopwatch.StartNew();
            LlmResult llmResult = await _llm
                .ProcessWithModelAsync(question, options.SystemPrompt, options.ModelName, Mode, cancellationToken)
                .ConfigureAwait(false);
            llmSw.Stop();
            total.Stop();

            if (!llmResult.IsSuccess)
            {
                Log.Warning("AskPipeline: LLM returned empty answer");
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "LLM returned empty answer");
                Completed?.Invoke(this, empty);
                return empty;
            }

            Log.Information("AskPipeline: answer received ({Chars} chars), total={Total}ms",
                llmResult.Text.Length, total.ElapsedMilliseconds);

            SetState(PipelineState.Idle);

            var result = new PipelineResult
            {
                Text = llmResult.Text,
                RawTranscript = question,
                Mode = Mode,
                IsSuccess = true,
                RecordingMs = options.RecordingDurationMs,
                TranscriptionMs = sttSw.ElapsedMilliseconds,
                ProcessingMs = llmSw.ElapsedMilliseconds,
                TotalMs = options.RecordingDurationMs + total.ElapsedMilliseconds,
                SttProvider = sttResult.Provider,
                LlmProvider = llmResult.Provider,
            };
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("AskPipeline: cancelled");
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AskPipeline: unhandled error");
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
        }
    }

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);
}
