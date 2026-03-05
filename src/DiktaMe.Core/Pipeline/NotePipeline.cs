
using System.Diagnostics;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using DiktaMe.Core.STT;
using Serilog;

namespace DiktaMe.Core.Pipeline;
/// <summary>
/// Orchestrates the Note-taking flow:
/// Record → STT → optional LLM formatting → append to notes file.
/// Port of V1's <c>PipelineExecutor.process_note_recording</c> in pipelines.py.
/// </summary>
public sealed class NotePipeline
{
    private readonly ISTTProvider _stt;
    private readonly ILLMProvider? _llm;
    private readonly SnippetManager? _snippets;

    /// <summary>Raised when the pipeline transitions to a new stage.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Raised when the pipeline completes (success or failure).</summary>
    public event EventHandler<PipelineResult>? Completed;

    /// <param name="stt">STT provider or router.</param>
    /// <param name="llm">
    /// Optional LLM for note formatting.
    /// When null (or when <see cref="NoteOptions.SystemPrompt"/> is empty), raw transcript is saved.
    /// </param>
    public NotePipeline(
        ISTTProvider stt,
        ILLMProvider? llm = null,
        SnippetManager? snippets = null)
    {
        _stt = stt;
        _llm = llm;
        _snippets = snippets;
    }

    /// <summary>
    /// Runs the Note pipeline for the supplied audio file.
    /// Never throws — all exceptions are caught and returned as a failed <see cref="PipelineResult"/>.
    /// </summary>
    public async Task<PipelineResult> RunAsync(
        string audioFilePath,
        NoteOptions options,
        CancellationToken cancellationToken = default)
    {
        const string Mode = "note";
        var total = Stopwatch.StartNew();
        var sttSw = new Stopwatch();

        try
        {
            // ── Stage 1: Transcribe ───────────────────────────────────────
            SetState(PipelineState.Transcribing);
            Log.Information("NotePipeline: transcribing");

            sttSw = Stopwatch.StartNew();
            TranscriptionResult sttResult = await _stt
                .TranscribeAsync(audioFilePath, options.Language, cancellationToken)
                .ConfigureAwait(false);
            sttSw.Stop();

            if (!sttResult.IsSuccess)
            {
                Log.Information("NotePipeline: empty transcription — aborting");
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "Empty transcription");
                Completed?.Invoke(this, empty);
                return empty;
            }

            string rawText = sttResult.Text;

            // ── Stage 2: LLM formatting (optional) ───────────────────────
            SetState(PipelineState.Processing);

            string noteText = rawText;
            long processingMs = 0;
            string? llmProvider = null;

            bool useLlm = _llm is not null && !string.IsNullOrWhiteSpace(options.SystemPrompt);
            if (useLlm)
            {
                Log.Information("NotePipeline: formatting via LLM ({Provider})", _llm!.ProviderName);
                var llmSw = Stopwatch.StartNew();
                LlmResult llmResult = await _llm!
                    .ProcessWithModelAsync(rawText, options.SystemPrompt!, options.ModelName, Mode, cancellationToken)
                    .ConfigureAwait(false);
                llmSw.Stop();
                processingMs = llmSw.ElapsedMilliseconds;
                llmProvider = llmResult.Provider;

                if (llmResult.IsSuccess)
                {
                    noteText = llmResult.Text;
                }
                else
                {
                    Log.Warning("NotePipeline: LLM returned empty — saving raw transcript");
                }
            }

            // ── Stage 3: Snippet expansion (post-LLM, pre-save) ──────────
            if (_snippets is not null)
            {
                noteText = _snippets.ExpandSnippets(noteText);
            }

            // ── Stage 4: Append to notes file ─────────────────────────────
            Log.Information("NotePipeline: saving note to '{Path}'", options.NotesFilePath);

            string timestamp = DateTime.Now.ToString(options.TimestampFormat, System.Globalization.CultureInfo.InvariantCulture);
            string entry = $"\n## {timestamp}\n\n{noteText}\n";

            string directory = Path.GetDirectoryName(options.NotesFilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.AppendAllTextAsync(options.NotesFilePath, entry, cancellationToken)
                .ConfigureAwait(false);

            total.Stop();
            Log.Information(
                "NotePipeline: complete — total={Total}ms stt={Stt}ms llm={Llm}ms",
                total.ElapsedMilliseconds, sttSw.ElapsedMilliseconds, processingMs);

            SetState(PipelineState.Idle);

            var result = new PipelineResult
            {
                Text = noteText,
                RawTranscript = rawText,
                Mode = Mode,
                IsSuccess = true,
                RecordingMs = options.RecordingDurationMs,
                TranscriptionMs = sttSw.ElapsedMilliseconds,
                ProcessingMs = processingMs,
                TotalMs = options.RecordingDurationMs + total.ElapsedMilliseconds,
                SttProvider = sttResult.Provider,
                LlmProvider = llmProvider,
            };
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("NotePipeline: cancelled");
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NotePipeline: unhandled error");
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
        }
    }

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);
}
