using System.Diagnostics;
using DiktaMe.Core.Config;
using DiktaMe.Core.Security;
using DiktaMe.Core.TTS;
using Serilog;

namespace DiktaMe.Core.Pipeline;

/// <summary>
/// Orchestrates the "Read Selection" flow:
/// clean text → synthesize via TTS → play audio.
/// Text capture (<see cref="Input.TextInjector.CaptureSelection"/>) is done
/// by the caller before invoking this pipeline — keeps the pipeline testable.
/// </summary>
public sealed class ReadSelectionPipeline
{
    private const string Mode = "read_selection";

    private readonly ITTSProvider _ttsProvider;
    private readonly ITtsPlayerService _player;
    private readonly SettingsManager _settings;

    public event EventHandler<PipelineState>? StateChanged;
    public event EventHandler<PipelineResult>? Completed;

    public ReadSelectionPipeline(
        ITTSProvider ttsProvider,
        ITtsPlayerService player,
        SettingsManager settings)
    {
        _ttsProvider = ttsProvider;
        _player = player;
        _settings = settings;
    }

    /// <summary>
    /// Cleans, synthesizes, and plays the selected text.
    /// </summary>
    /// <param name="selectedText">Text captured from the user's selection.</param>
    /// <param name="cancellationToken">Cancels synthesis or playback when triggered.</param>
    public async Task<PipelineResult> RunAsync(
        string selectedText,
        CancellationToken cancellationToken = default)
    {
        var total = Stopwatch.StartNew();

        try
        {
            // ── Validate input ──────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "No text selected");
                Completed?.Invoke(this, empty);
                return empty;
            }

            // ── Stage 1: Clean text for speech ──────────────────────────
            SetState(PipelineState.Processing);
            var ttsSettings = _settings.Current.Tts;
            string cleaned = TextCleaner.CleanForSpeech(selectedText, ttsSettings.MaxSpeechWords);
            LogUserText("ReadSelectionPipeline: cleaned text", cleaned);

            if (string.IsNullOrWhiteSpace(cleaned))
            {
                SetState(PipelineState.Idle);
                var empty = PipelineResult.Failure(Mode, "Text empty after cleaning");
                Completed?.Invoke(this, empty);
                return empty;
            }

            // ── Stage 2: Synthesize ─────────────────────────────────────
            string? voiceId = string.IsNullOrEmpty(ttsSettings.VoiceId) ? null : ttsSettings.VoiceId;
            var synthSw = Stopwatch.StartNew();
            TtsResult ttsResult = await _ttsProvider
                .SynthesizeAsync(cleaned, voiceId, cancellationToken)
                .ConfigureAwait(false);
            synthSw.Stop();

            if (!ttsResult.IsSuccess)
            {
                Log.Warning("ReadSelectionPipeline: synthesis returned empty audio");
                SetState(PipelineState.Idle);
                var fail = PipelineResult.Failure(Mode, "TTS synthesis returned no audio");
                Completed?.Invoke(this, fail);
                return fail;
            }

            Log.Information(
                "ReadSelectionPipeline: synthesized {Chars} chars in {Ms}ms via {Provider}",
                cleaned.Length, synthSw.ElapsedMilliseconds, ttsResult.Provider);

            // ── Stage 3: Play audio ─────────────────────────────────────
            SetState(PipelineState.Speaking);
            _player.Volume = ttsSettings.VolumePercent / 100f;

            var playSw = Stopwatch.StartNew();
            await _player.PlayAsync(ttsResult.AudioData, ttsResult.SampleRate, cancellationToken)
                .ConfigureAwait(false);
            playSw.Stop();

            total.Stop();
            SetState(PipelineState.Idle);

            var result = new PipelineResult
            {
                Text = cleaned,
                RawTranscript = selectedText,
                Mode = Mode,
                IsSuccess = true,
                ProcessingMs = synthSw.ElapsedMilliseconds,
                TtsPlayedMs = synthSw.ElapsedMilliseconds + playSw.ElapsedMilliseconds,
                TotalMs = total.ElapsedMilliseconds,
            };
            Completed?.Invoke(this, result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("ReadSelectionPipeline: cancelled");
            _player.Stop();
            SetState(PipelineState.Idle);
            var r = PipelineResult.Failure(Mode, "Cancelled");
            Completed?.Invoke(this, r);
            return r;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ReadSelectionPipeline: unhandled error");
            _player.Stop();
            SetState(PipelineState.Error);
            var r = PipelineResult.Failure(Mode, ex.Message);
            Completed?.Invoke(this, r);
            return r;
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
            return;
        }

        string loggedText = text;

        if (level == PrivacyLevel.Balanced && _settings.Current.Privacy.PiiScrubEnabled)
        {
            if (PIIScrubber.ContainsPII(text))
            {
                Log.Warning("{Prefix}: contains PII (scrubbed in log)", prefix);
            }

            loggedText = PIIScrubber.Scrub(text);
        }

        Log.Information("{Prefix} = '{Text}'", prefix, loggedText);
    }
}
