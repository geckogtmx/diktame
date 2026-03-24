
using System.Diagnostics;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Security;
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
    private readonly SettingsManager _settings;

    /// <summary>Raised when the pipeline transitions to a new stage.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Raised when the pipeline completes (success or failure).</summary>
    public event EventHandler<PipelineResult>? Completed;

    /// <param name="llm">LLM provider (or router) for chat responses.</param>
    /// <param name="settings">Settings manager for privacy-aware logging.</param>
    /// <param name="stt">
    /// Optional STT provider for voice input.
    /// Required when <see cref="ChatOptions.AudioFilePath"/> is set.
    /// Null is valid for text-only usage.
    /// </param>
    private readonly PipelineEventBus? _eventBus;

    public ChatPipeline(ILLMProvider llm, SettingsManager settings, ISTTProvider? stt = null, PipelineEventBus? eventBus = null)
    {
        _llm = llm;
        _settings = settings;
        _stt = stt;
        _eventBus = eventBus;
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
            double? audioDurationSec = null;

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
                audioDurationSec = sttResult.AudioDurationSec;

                if (!sttResult.IsSuccess)
                {
                    Log.Information("ChatPipeline: empty transcription — aborting");
                    SetState(PipelineState.Idle);
                    var empty = PipelineResult.Failure(Mode, "No question detected");
                    Completed?.Invoke(this, empty);
                    return empty;
                }

                question = sttResult.Text;
                LogUserText("ChatPipeline: transcribed question", question);
            }
            else
            {
                throw new ArgumentException(
                    "ChatOptions must supply either TextInput or AudioFilePath.", nameof(options));
            }

            // ── LLM chat response ─────────────────────────────────────────
            SetState(PipelineState.Processing);
            ApplyProviderSettings(options);

            var effectivePrompt = options.SystemPrompt;
            if (_eventBus is not null)
            {
                var ctx = new BeforeLlmContext { UserText = question, SystemPrompt = effectivePrompt, Mode = Mode };
                _eventBus.PublishBeforeLlm(ctx);
                if (ctx.AdditionalSystemContext.Length > 0)
                    effectivePrompt = $"{effectivePrompt}\n\n{ctx.AdditionalSystemContext}";
            }

            Log.Information("ChatPipeline: sending to LLM ({Provider})", _llm.ProviderName);

            var llmSw = Stopwatch.StartNew();
            LlmResult llmResult = await _llm
                .ProcessWithModelAsync(question, effectivePrompt, options.ModelName, Mode, cancellationToken)
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
                AudioDurationSec = audioDurationSec,
                TokensPerSec = llmResult.TokensPerSec,
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

    /// <summary>
    /// Runs the Chat pipeline with multi-turn conversation history.
    /// Truncates the history to fit within the context window budget,
    /// then delegates to <see cref="ILLMProvider.ProcessConversationAsync"/>.
    /// Text-only (no STT path).
    /// </summary>
    public async Task<PipelineResult> RunConversationAsync(
        ChatOptions options,
        IReadOnlyList<ConversationTurn> history,
        int? contextWindowTokens = null,
        CancellationToken cancellationToken = default)
    {
        const string Mode = "chat";
        var total = Stopwatch.StartNew();

        try
        {
            if (history.Count == 0)
            {
                Log.Information("ChatPipeline: empty conversation history — aborting");
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "No messages in conversation");
                Completed?.Invoke(this, empty);
                return empty;
            }

            // ── Context window truncation ────────────────────────────────
            var truncated = TruncateHistory(history, options.SystemPrompt, contextWindowTokens ?? 8192);

            // ── LLM conversation response ────────────────────────────────
            SetState(PipelineState.Processing);
            ApplyProviderSettings(options);

            var convEffectivePrompt = options.SystemPrompt;
            if (_eventBus is not null)
            {
                var lastUserContent = truncated.LastOrDefault(t => string.Equals(t.Role, "user", StringComparison.Ordinal))?.Content ?? string.Empty;
                var ctx = new BeforeLlmContext
                {
                    UserText = lastUserContent,
                    SystemPrompt = convEffectivePrompt,
                    Mode = Mode,
                };
                _eventBus.PublishBeforeLlm(ctx);
                if (ctx.AdditionalSystemContext.Length > 0)
                    convEffectivePrompt = $"{convEffectivePrompt}\n\n{ctx.AdditionalSystemContext}";
            }

            Log.Information("ChatPipeline: sending {Count} turns to LLM ({Provider})",
                truncated.Count, _llm.ProviderName);

            var llmSw = Stopwatch.StartNew();
            LlmResult llmResult = await _llm
                .ProcessConversationWithModelAsync(truncated, convEffectivePrompt,
                    options.ModelName, Mode, cancellationToken)
                .ConfigureAwait(false);
            llmSw.Stop();
            total.Stop();

            if (!llmResult.IsSuccess)
            {
                Log.Warning("ChatPipeline: LLM returned empty conversation answer");
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "LLM returned empty answer");
                Completed?.Invoke(this, empty);
                return empty;
            }

            Log.Information("ChatPipeline: conversation answer ({Chars} chars), total={Total}ms",
                llmResult.Text.Length, total.ElapsedMilliseconds);

            SetState(PipelineState.Idle);

            var result = new PipelineResult
            {
                Text = llmResult.Text,
                RawTranscript = history[^1].Content,
                Mode = Mode,
                IsSuccess = true,
                TranscriptionMs = 0,
                ProcessingMs = llmSw.ElapsedMilliseconds,
                TotalMs = total.ElapsedMilliseconds,
                LlmProvider = llmResult.Provider,
                InputTokens = llmResult.InputTokens,
                OutputTokens = llmResult.OutputTokens,
                TokensPerSec = llmResult.TokensPerSec,
            };
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("ChatPipeline: conversation cancelled");
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChatPipeline: conversation unhandled error");
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
        }
    }

    /// <summary>
    /// Truncates conversation history to fit within the context window.
    /// Keeps the most recent messages that fit within 80% of the budget
    /// (reserving 20% for the response). System prompt tokens are subtracted first.
    /// Token estimation: 1 token ≈ 4 characters.
    /// </summary>
    internal static IReadOnlyList<ConversationTurn> TruncateHistory(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        int contextWindowTokens)
    {
        const double ResponseReserve = 0.20;
        const int CharsPerToken = 4;

        int budget = (int)(contextWindowTokens * (1.0 - ResponseReserve));
        int systemTokens = systemPrompt.Length / CharsPerToken;
        budget -= systemTokens;

        if (budget <= 0)
        {
            // System prompt alone exceeds budget — return just the last message
            return history.Count > 0 ? [history[^1]] : [];
        }

        // Walk backwards, accumulating messages until budget is exhausted
        var selected = new List<ConversationTurn>();
        int usedTokens = 0;

        for (int i = history.Count - 1; i >= 0; i--)
        {
            int msgTokens = history[i].Content.Length / CharsPerToken;
            if (usedTokens + msgTokens > budget && selected.Count > 0)
            {
                break; // Would exceed budget, stop (but always include at least one)
            }

            selected.Add(history[i]);
            usedTokens += msgTokens;
        }

        selected.Reverse(); // Restore chronological order
        return selected;
    }

    /// <summary>
    /// Applies per-request chat settings to the LLM provider.
    /// Currently sets Gemini web search grounding when enabled.
    /// </summary>
    private void ApplyProviderSettings(ChatOptions options)
    {
        if (_llm is GeminiProvider gemini)
        {
            gemini.WebSearchEnabled = options.WebSearchEnabled;
        }
    }

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);

    /// <summary>
    /// Logs user text respecting privacy settings:
    /// - Ghost: No logging
    /// - Stats: No text logging (metrics only)
    /// - Balanced: Log with PII scrubbing (if enabled)
    /// - Full: Log verbatim
    /// </summary>
    private void LogUserText(string prefix, string text)
    {
        var level = _settings.Current.Privacy.Level;

        if (level == PrivacyLevel.Ghost || level == PrivacyLevel.Stats)
        {
            // Ghost and Stats: don't log user text
            return;
        }

        string loggedText = text;

        if (level == PrivacyLevel.Balanced && _settings.Current.Privacy.PiiScrubEnabled)
        {
            // Balanced with PII scrubbing enabled: scrub before logging
            if (PIIScrubber.ContainsPII(text))
            {
                Log.Warning("{Prefix}: contains PII (scrubbed in log)", prefix);
            }
            loggedText = PIIScrubber.Scrub(text);
        }

        // Full level or Balanced with scrubbing disabled: log as-is
        Log.Information("{Prefix} = '{Text}'", prefix, loggedText);
    }
}
