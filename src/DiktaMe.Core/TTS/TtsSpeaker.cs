using System.Diagnostics;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Reusable TTS service: clean text → synthesize → play with optional ducking.
/// Used by pipeline handlers (Ask, Chat, Translate) and NotificationService.
/// Follows the pattern proven in <see cref="Pipeline.ReadSelectionPipeline"/>.
/// </summary>
public sealed class TtsSpeaker
{
    private readonly ITTSProviderFactory _factory;
    private readonly ITtsPlayerService _player;
    private readonly SettingsManager _settings;
    private readonly AudioDucker _ducker;

    public TtsSpeaker(
        ITTSProviderFactory factory,
        ITtsPlayerService player,
        SettingsManager settings,
        AudioDucker ducker)
    {
        _factory = factory;
        _player = player;
        _settings = settings;
        _ducker = ducker;
    }

    /// <summary>
    /// Speaks text aloud unconditionally (caller decides when to call).
    /// Returns elapsed TTS milliseconds (0 on skip/failure/cancel).
    /// Never throws — exceptions are caught and logged.
    /// </summary>
    public async Task<long> SpeakAsync(string text, CancellationToken ct = default)
    {
        var tts = _settings.Current.Tts;
        string cleaned = TextCleaner.CleanForSpeech(text, tts.MaxSpeechWords);
        if (string.IsNullOrWhiteSpace(cleaned))
            return 0;

        bool shouldDuck = tts.DuckDuringPlayback && _settings.Current.AudioDucking.Enabled;
        if (shouldDuck)
        {
            _ducker.IsEnabled = true;
            _ducker.DuckLevel = _settings.Current.AudioDucking.DuckLevelPercent / 100f;
            _ducker.Duck();
        }

        try
        {
            string? voiceId = string.IsNullOrEmpty(tts.VoiceId) ? null : tts.VoiceId;
            // Only pass KokoroModelVariant for kokoro — cloud providers resolve their own model via ResolveVariant
            string? variant = string.Equals(tts.Provider, "kokoro", StringComparison.OrdinalIgnoreCase)
                ? tts.KokoroModelVariant : null;
            ITTSProvider provider = _factory.CreateProvider(tts.Provider, variant);

            var sw = Stopwatch.StartNew();
            TtsResult result = await provider
                .SynthesizeAsync(cleaned, voiceId, ct)
                .ConfigureAwait(false);

            if (!result.IsSuccess)
            {
                Log.Warning("TtsSpeaker: synthesis returned empty audio");
                return 0;
            }

            _player.Volume = tts.VolumePercent / 100f;
            await _player.PlayAsync(result.AudioData, result.SampleRate, ct)
                .ConfigureAwait(false);

            sw.Stop();
            Log.Information("TtsSpeaker: played {Chars} chars in {Ms}ms via {Provider}",
                cleaned.Length, sw.ElapsedMilliseconds, result.Provider);
            return sw.ElapsedMilliseconds;
        }
        catch (OperationCanceledException)
        {
            _player.Stop();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TtsSpeaker: synthesis/playback failed");
            _player.Stop();
            return 0;
        }
        finally
        {
            if (shouldDuck)
                _ducker.Restore();
        }
    }

    /// <summary>
    /// Speaks text only if TTS is globally enabled AND the per-mode toggle is on.
    /// </summary>
    /// <param name="text">Text to speak (cleaned internally via <see cref="TextCleaner"/>).</param>
    /// <param name="mode">Mode key: "ask", "chat", "translate", "notification".</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Elapsed TTS milliseconds (0 if skipped or failed).</returns>
    public async Task<long> SpeakIfEnabledAsync(string text, string mode, CancellationToken ct = default)
    {
        var tts = _settings.Current.Tts;
        if (!tts.Enabled)
            return 0;

        bool modeEnabled = mode switch
        {
            "ask" => tts.SpeakAskResponses,
            "chat" => tts.SpeakChatResponses,
            "translate" => tts.SpeakTranslations,
            "notification" => tts.SpeakNotifications,
            _ => false,
        };

        if (!modeEnabled)
            return 0;

        return await SpeakAsync(text, ct).ConfigureAwait(false);
    }
}
