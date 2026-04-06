using System.Diagnostics;
using System.Text;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.Input;
using DiktaMe.Core.STT;
using Serilog;

namespace DiktaMe.Core.Pipeline;

/// <summary>
/// Orchestrates real-time streaming dictation: audio chunks are forwarded to a
/// streaming STT provider over WebSocket and final transcripts are injected
/// immediately as they arrive. Always operates in raw mode (no LLM).
/// </summary>
public sealed class StreamingDictationPipeline : IAsyncDisposable
{
    private const string Mode = "dictate";

    private readonly IStreamingSTTProvider _streamingStt;
    private readonly TextInjector _injector;
    private readonly SnippetManager? _snippets;
    private bool _disposed;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised when the pipeline moves to a new stage.</summary>
    public event EventHandler<PipelineState>? StateChanged;

    /// <summary>Raised when the pipeline completes (success or failure).</summary>
    public event EventHandler<PipelineResult>? Completed;

    public StreamingDictationPipeline(
        IStreamingSTTProvider streamingStt,
        TextInjector injector,
        SnippetManager? snippets = null)
    {
        _streamingStt = streamingStt;
        _injector = injector;
        _snippets = snippets;
    }

    /// <summary>
    /// Runs the streaming dictation pipeline. Connects the streaming STT provider,
    /// forwards audio chunks from <paramref name="audioSource"/>, and injects final
    /// transcripts as they arrive. Blocks until recording stops and remaining finals are drained.
    /// Never throws — all exceptions are caught and returned as a failed <see cref="PipelineResult"/>.
    /// </summary>
    public async Task<PipelineResult> RunAsync(
        IAudioDataSource audioSource,
        DictationOptions options,
        CancellationToken cancellationToken = default)
    {
        var total = Stopwatch.StartNew();
        var accumulated = new StringBuilder();
        long injectionMs = 0;
        var recordingStoppedTcs = new TaskCompletionSource<long>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            // ── Connect to streaming STT ───────────────────────────────────
            Log.Information("StreamingDictation: connecting to {Provider}", _streamingStt.ProviderName);
            await _streamingStt.ConnectAsync(options.Language, cancellationToken).ConfigureAwait(false);

            SetState(PipelineState.Streaming);
            Log.Information("StreamingDictation: connected, streaming audio");

            // ── Define event handlers ──────────────────────────────────────
            void OnAudioData(object? s, AudioDataAvailableEventArgs e)
                => ForwardAudioChunk(e, cancellationToken);

            void OnFinalTranscript(object? s, StreamingTranscriptEventArgs e)
                => HandleFinalTranscript(e, options, accumulated, ref injectionMs);

            void OnRecordingStopped(object? s, RecordingStoppedEventArgs e)
                => recordingStoppedTcs.TrySetResult(e.DurationMs);

            void OnError(object? s, StreamingErrorEventArgs e)
                => Log.Warning("StreamingDictation: provider error — {Message}", e.Message);

            // Subscribe
            audioSource.AudioDataAvailable += OnAudioData;
            audioSource.RecordingStopped += OnRecordingStopped;
            audioSource.AutoStopped += OnRecordingStopped;
            _streamingStt.FinalTranscriptReceived += OnFinalTranscript;
            _streamingStt.ErrorOccurred += OnError;

            long recordingMs;
            try
            {
                // ── Wait for recording to stop ─────────────────────────────
                recordingMs = await recordingStoppedTcs.Task
                    .WaitAsync(cancellationToken).ConfigureAwait(false);

                Log.Information("StreamingDictation: recording stopped ({Ms}ms), closing stream",
                    recordingMs);

                // ── Close streaming provider (drains remaining finals) ─────
                SetState(PipelineState.Injecting);
                await _streamingStt.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Unsubscribe
                audioSource.AudioDataAvailable -= OnAudioData;
                audioSource.RecordingStopped -= OnRecordingStopped;
                audioSource.AutoStopped -= OnRecordingStopped;
                _streamingStt.FinalTranscriptReceived -= OnFinalTranscript;
                _streamingStt.ErrorOccurred -= OnError;
            }

            // ── Build result ───────────────────────────────────────────────
            total.Stop();
            var result = BuildSuccessResult(accumulated, recordingMs, injectionMs, total.ElapsedMilliseconds);
            SetState(PipelineState.Idle);
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("StreamingDictation: cancelled");
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (DiktaMe.Core.Account.WalletStreamingFallbackException)
        {
            // Rethrow so the UI orchestration (LoadingViewModel) can intercept it
            // and perform its transparent fallback routine.
            SetState(PipelineState.Error);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StreamingDictation: unhandled error");
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _streamingStt.DisposeAsync().ConfigureAwait(false);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void SetState(PipelineState state) => StateChanged?.Invoke(this, state);

    private void ForwardAudioChunk(AudioDataAvailableEventArgs e, CancellationToken ct)
    {
        if (!_streamingStt.IsConnected)
        {
            return;
        }

        // Copy buffer — NAudio reuses it across callbacks
        byte[] copy = e.PcmData.ToArray();
        _ = SendAudioSafeAsync(copy, ct);
    }

    private void HandleFinalTranscript(
        StreamingTranscriptEventArgs e,
        DictationOptions options,
        StringBuilder accumulated,
        ref long injectionMs)
    {
        if (string.IsNullOrEmpty(e.Transcript))
        {
            return; // Skip empty finals (silence segments)
        }

        string text = e.Transcript;
        if (_snippets is not null)
        {
            text = _snippets.ExpandSnippets(text);
        }

        var injSw = Stopwatch.StartNew();
        _injector.InjectText(text, options.Injection.TrailingSpace, additionalKey: null);
        injSw.Stop();
        Interlocked.Add(ref injectionMs, injSw.ElapsedMilliseconds);

        accumulated.Append(text);
        if (options.Injection.TrailingSpace)
        {
            accumulated.Append(' ');
        }

        Log.Debug("StreamingDictation: injected \"{Preview}\" ({Ms}ms)",
            text.Length > 40 ? string.Concat(text.AsSpan(0, 40), "...") : text,
            injSw.ElapsedMilliseconds);
    }

    private PipelineResult BuildSuccessResult(
        StringBuilder accumulated, long recordingMs, long injectionMs, long totalMs)
    {
        string finalText = accumulated.ToString().TrimEnd();

        Log.Information(
            "StreamingDictation: complete — total={Total}ms rec={Rec}ms inj={Inj}ms words={Words}",
            totalMs, recordingMs, injectionMs,
            string.IsNullOrWhiteSpace(finalText)
                ? 0
                : finalText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

        return new PipelineResult
        {
            Text = finalText,
            RawTranscript = finalText,
            Mode = Mode,
            IsSuccess = true,
            RecordingMs = recordingMs,
            TranscriptionMs = 0,
            ProcessingMs = 0,
            InjectionMs = injectionMs,
            TotalMs = totalMs,
            SttProvider = _streamingStt.ProviderName,
            LlmProvider = null,
        };
    }

    private async Task SendAudioSafeAsync(byte[] pcmData, CancellationToken cancellationToken)
    {
        try
        {
            await _streamingStt.SendAudioAsync(pcmData, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Warning(ex, "StreamingDictation: failed to send audio chunk");
        }
    }
}
