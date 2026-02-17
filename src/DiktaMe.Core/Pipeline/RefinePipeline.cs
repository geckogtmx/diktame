namespace DiktaMe.Core.Pipeline;

using System.Diagnostics;
using DiktaMe.Core.Config;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.STT;
using Serilog;

/// <summary>
/// Orchestrates the Refine flow:
/// <list type="bullet">
///   <item><description>
///     <b>Autopilot</b>: Capture selection → LLM cleanup → replace in-place.
///   </description></item>
///   <item><description>
///     <b>Instruction</b>: Transcribe audio → capture selection → LLM (selection + instruction) → replace.
///     Falls back to Ask mode when no text is selected.
///   </description></item>
/// </list>
/// Port of V1's <c>PipelineExecutor.process_refine_recording</c> in pipelines.py.
/// </summary>
public sealed class RefinePipeline
{
    private readonly ISTTProvider? _stt;       // null in autopilot mode
    private readonly ILLMProvider _llm;
    private readonly TextInjector _injector;
    private readonly SnippetManager? _snippets;

    /// <summary>Raised when the pipeline transitions to a new stage.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Raised when the pipeline completes (success or failure).</summary>
    public event EventHandler<PipelineResult>? Completed;

    /// <param name="llm">LLM provider or router.</param>
    /// <param name="injector">Text injector.</param>
    /// <param name="stt">
    /// STT provider for instruction mode.
    /// Pass null to use autopilot mode (no audio recording step).
    /// </param>
    public RefinePipeline(
        ILLMProvider llm,
        TextInjector injector,
        ISTTProvider? stt = null,
        SnippetManager? snippets = null)
    {
        _llm = llm;
        _injector = injector;
        _stt = stt;
        _snippets = snippets;
    }

    /// <summary>
    /// Runs the refine pipeline.
    /// Never throws — all exceptions are caught and returned as a failed <see cref="PipelineResult"/>.
    /// </summary>
    /// <param name="audioFilePath">
    /// Path to the instruction audio file.
    /// Must be non-null in instruction mode (<see cref="_stt"/> provided);
    /// ignored in autopilot mode.
    /// </param>
    /// <param name="options">Refine options.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task<PipelineResult> RunAsync(
        string? audioFilePath,
        RefineOptions options,
        CancellationToken cancellationToken = default)
    {
        const string Mode = "refine";
        var total = Stopwatch.StartNew();

        try
        {
            string? instruction = null;
            long transcriptionMs = 0;
            string? sttProvider = null;

            // ── Stage 1 (instruction mode only): transcribe audio ─────────
            if (_stt is not null && !string.IsNullOrEmpty(audioFilePath))
            {
                SetState(PipelineState.Transcribing);
                Log.Information("RefinePipeline: transcribing instruction audio");

                var sttSw = Stopwatch.StartNew();
                TranscriptionResult sttResult = await _stt
                    .TranscribeAsync(audioFilePath, options.Language, cancellationToken)
                    .ConfigureAwait(false);
                sttSw.Stop();

                transcriptionMs = sttSw.ElapsedMilliseconds;
                sttProvider = sttResult.Provider;

                if (!sttResult.IsSuccess)
                {
                    Log.Warning("RefinePipeline: empty instruction — aborting");
                    SetState(PipelineState.Idle);
                    var emptyResult = PipelineResult.Failure(Mode, "No instruction detected");
                    Completed?.Invoke(this, emptyResult);
                    return emptyResult;
                }

                instruction = sttResult.Text;
                Log.Information("RefinePipeline: instruction = '{Instruction}'", instruction);
            }

            // ── Stage 2: Capture selection ────────────────────────────────
            SetState(PipelineState.Processing);
            Log.Information("RefinePipeline: capturing selection");

            string? selectedText = _injector.CaptureSelection();

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                // No selection: in instruction mode fall back to Ask; in autopilot abort.
                if (instruction is not null)
                {
                    Log.Information("RefinePipeline: no selection — falling back to Ask mode");
                    return await FallbackToAskAsync(
                        instruction, options, transcriptionMs, sttProvider, total, cancellationToken)
                        .ConfigureAwait(false);
                }

                Log.Warning("RefinePipeline: no text selected (autopilot) — aborting");
                SetState(PipelineState.Idle);
                var noSelResult = PipelineResult.Failure(Mode, "No text selected");
                Completed?.Invoke(this, noSelResult);
                return noSelResult;
            }

            Log.Information("RefinePipeline: captured {Chars} chars of selected text", selectedText.Length);

            // ── Stage 3: Build prompt and call LLM ────────────────────────
            string effectivePrompt = instruction is not null
                ? options.SystemPrompt.Replace("{instruction}", instruction, StringComparison.Ordinal)
                : options.SystemPrompt;

            Log.Information("RefinePipeline: calling LLM ({Provider})", _llm.ProviderName);
            var llmSw = Stopwatch.StartNew();
            LlmResult llmResult = await _llm
                .ProcessAsync(selectedText, effectivePrompt, Mode, cancellationToken)
                .ConfigureAwait(false);
            llmSw.Stop();

            if (!llmResult.IsSuccess)
            {
                Log.Warning("RefinePipeline: LLM returned empty result");
                SetState(PipelineState.Idle);
                var emptyLlm = PipelineResult.Failure(Mode, "LLM returned empty result");
                Completed?.Invoke(this, emptyLlm);
                return emptyLlm;
            }

            string refinedText = llmResult.Text;

            // ── Stage 4: Snippet expansion (post-LLM, pre-inject) ────────
            if (_snippets is not null)
                refinedText = _snippets.ExpandSnippets(refinedText);

            // ── Stage 5: Inject ───────────────────────────────────────────
            SetState(PipelineState.Injecting);
            Log.Information("RefinePipeline: injecting {Chars} chars refined text", refinedText.Length);

            var injSw = Stopwatch.StartNew();
            _injector.InjectText(
                refinedText,
                options.Injection.TrailingSpace,
                options.Injection.AdditionalKey);
            injSw.Stop();

            total.Stop();
            Log.Information(
                "RefinePipeline: complete — total={Total}ms stt={Stt}ms llm={Llm}ms inj={Inj}ms",
                total.ElapsedMilliseconds, transcriptionMs,
                llmSw.ElapsedMilliseconds, injSw.ElapsedMilliseconds);

            SetState(PipelineState.Idle);

            var result = new PipelineResult
            {
                Text = refinedText,
                RawTranscript = instruction,   // instruction text (null in autopilot)
                Mode = Mode,
                IsSuccess = true,
                TranscriptionMs = transcriptionMs,
                ProcessingMs = llmSw.ElapsedMilliseconds,
                InjectionMs = injSw.ElapsedMilliseconds,
                TotalMs = total.ElapsedMilliseconds,
                SttProvider = sttProvider,
                LlmProvider = llmResult.Provider,
            };
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("RefinePipeline: cancelled");
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "RefinePipeline: unhandled error");
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// When refine finds no selection, treat the spoken instruction as an Ask question.
    /// The LLM answer is written to clipboard rather than injected (matches V1 behaviour).
    /// </summary>
    private async Task<PipelineResult> FallbackToAskAsync(
        string instruction,
        RefineOptions options,
        long transcriptionMs,
        string? sttProvider,
        Stopwatch total,
        CancellationToken cancellationToken)
    {
        const string Mode = "refine_ask_fallback";

        var llmSw = Stopwatch.StartNew();
        LlmResult llmResult = await _llm
            .ProcessAsync(instruction, options.SystemPrompt, "ask", cancellationToken)
            .ConfigureAwait(false);
        llmSw.Stop();
        total.Stop();

        SetState(PipelineState.Idle);

        var result = new PipelineResult
        {
            Text = llmResult.Text,
            RawTranscript = instruction,
            Mode = Mode,
            IsSuccess = llmResult.IsSuccess,
            ErrorMessage = llmResult.IsSuccess ? null : "LLM returned empty result in fallback",
            TranscriptionMs = transcriptionMs,
            ProcessingMs = llmSw.ElapsedMilliseconds,
            TotalMs = total.ElapsedMilliseconds,
            SttProvider = sttProvider,
            LlmProvider = llmResult.Provider,
        };
        Completed?.Invoke(this, result);
        return result;
    }

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);
}
