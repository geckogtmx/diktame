
using System.Diagnostics;
using DiktaMe.Core.LLM;
using DiktaMe.Core.STT;
using Serilog;

namespace DiktaMe.Core.Pipeline;
/// <summary>
/// Orchestrates the Quick Chat flow: optional STT (voice input) → LLM Q&amp;A → return answer.
/// Unlike <see cref="AskPipeline"/>, the input may be plain text (typed in the overlay) or
/// audio (Mic button). Text input bypasses the STT stage entirely.
/// The response is returned in the <see cref="PipelineResult"/> and is NOT injected —
/// the Quick Chat overlay displays it directly.
/// Port of V1 SPEC_042d (Quick Chat Overlay core).
/// </summary>
/// <remarks>
/// Voice input uses raw STT (no LLM cleanup of the question) per SPEC_042d step 7.
/// UI bindings (QuickChatView.xaml) are wired in Stream F.
/// </remarks>
public sealed class ChatPipeline
{
    private readonly ISTTProvider? _stt;
    private readonly ILLMProvider _llm;

    /// <summary>Raised when the pipeline transitions to a new stage.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Raised when the pipeline completes (success or failure).</summary>
    public event EventHandler<PipelineResult>? Completed;

    /// <param name="llm">LLM provider (or router) for chat responses.</param>
    /// <param name="stt">
    /// Optional STT provider for voice input.
    /// Required when <see cref="ChatOptions.AudioFilePath"/> is set.
    /// Null is valid for text-only usage.
    /// </param>
    public ChatPipeline(ILLMProvider llm, ISTTProvider? stt = null)
    {
        _llm = llm;
        _stt = stt;
    }

    /// <summary>
    /// Runs the Chat pipeline.
    /// Accepts either a text question (<see cref="ChatOptions.TextInput"/>) or an
    /// audio file (<see cref="ChatOptions.AudioFilePath"/>).
    /// Never throws — all exceptions are caught and returned as a failed <see cref="PipelineResult"/>.
    /// </summary>
    public async Task<PipelineResult> RunAsync(
        ChatOptions options,
        CancellationToken cancellationToken = default)
    {
        const string Mode = "chat";
        var total = Stopwatch.StartNew();

        try
        {
            string question;
            string? sttProvider = null;
            long sttMs = 0;

            if (options.TextInput is not null)
            {
                // ── Text path: no STT ─────────────────────────────────────
                question = options.TextInput.Trim();
                Log.Information("ChatPipeline: text input ({Chars} chars)", question.Length);

                if (string.IsNullOrWhiteSpace(question))
                {
                    Log.Information("ChatPipeline: empty text input — aborting");
                    SetState(PipelineState.Idle);
                    var empty = PipelineResult.Failure(Mode, "No question provided");
                    Completed?.Invoke(this, empty);
                    return empty;
                }
            }
            else if (options.AudioFilePath is not null)
            {
                // ── Voice path: raw STT (no LLM cleanup of question) ─────
                if (_stt is null)
                {
                    throw new InvalidOperationException(
                        "ChatPipeline: an ISTTProvider is required when AudioFilePath is set.");
                }

                SetState(PipelineState.Transcribing);
                Log.Information("ChatPipeline: transcribing voice input");

                var sttSw = Stopwatch.StartNew();
                TranscriptionResult sttResult = await _stt
                    .TranscribeAsync(options.AudioFilePath, options.Language, cancellationToken)
                    .ConfigureAwait(false);
                sttSw.Stop();
                sttMs = sttSw.ElapsedMilliseconds;
                sttProvider = sttResult.Provider;

                if (!sttResult.IsSuccess)
                {
                    Log.Information("ChatPipeline: empty transcription — aborting");
                    SetState(PipelineState.Idle);
                    var empty = PipelineResult.Failure(Mode, "No question detected");
                    Completed?.Invoke(this, empty);
                    return empty;
                }

                question = sttResult.Text;
                Log.Information("ChatPipeline: transcribed question = '{Question}'", question);
            }
            else
            {
                throw new ArgumentException(
                    "ChatOptions must supply either TextInput or AudioFilePath.", nameof(options));
            }

            // ── LLM chat response ─────────────────────────────────────────
            SetState(PipelineState.Processing);
            Log.Information("ChatPipeline: sending to LLM ({Provider})", _llm.ProviderName);

            var llmSw = Stopwatch.StartNew();
            LlmResult llmResult = await _llm
                .ProcessWithModelAsync(question, options.SystemPrompt, options.ModelName, Mode, cancellationToken)
                .ConfigureAwait(false);
            llmSw.Stop();
            total.Stop();

            if (!llmResult.IsSuccess)
            {
                Log.Warning("ChatPipeline: LLM returned empty answer");
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "LLM returned empty answer");
                Completed?.Invoke(this, empty);
                return empty;
            }

            Log.Information("ChatPipeline: answer ({Chars} chars), total={Total}ms",
                llmResult.Text.Length, total.ElapsedMilliseconds);

            SetState(PipelineState.Idle);

            var result = new PipelineResult
            {
                Text = llmResult.Text,
                RawTranscript = question,
                Mode = Mode,
                IsSuccess = true,
                TranscriptionMs = sttMs,
                ProcessingMs = llmSw.ElapsedMilliseconds,
                TotalMs = total.ElapsedMilliseconds,
                SttProvider = sttProvider,
                LlmProvider = llmResult.Provider,
            };
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("ChatPipeline: cancelled");
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChatPipeline: unhandled error");
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
        }
    }

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);
}
