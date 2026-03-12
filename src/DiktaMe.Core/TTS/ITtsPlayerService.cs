namespace DiktaMe.Core.TTS;

/// <summary>
/// Abstraction for TTS audio playback with transport controls.
/// Implemented by <see cref="TtsPlayerService"/>.
/// </summary>
public interface ITtsPlayerService : IDisposable
{
    /// <summary>Whether audio is currently playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Whether audio playback is paused.</summary>
    bool IsPaused { get; }

    /// <summary>Current playback position.</summary>
    TimeSpan Position { get; }

    /// <summary>Total audio duration.</summary>
    TimeSpan Duration { get; }

    /// <summary>Playback volume (0.0–1.0). Can be changed while playing.</summary>
    float Volume { get; set; }

    /// <summary>Raised when playback starts.</summary>
    event EventHandler? PlaybackStarted;

    /// <summary>Raised when playback reaches the end of the audio naturally.</summary>
    event EventHandler? PlaybackFinished;

    /// <summary>Raised when playback is stopped by user action (Stop() call).</summary>
    event EventHandler? PlaybackStopped;

    /// <summary>
    /// Plays raw PCM audio data. Stops any current playback first.
    /// Returns when playback completes or is stopped.
    /// </summary>
    Task PlayAsync(byte[] audioData, int sampleRate, CancellationToken cancellationToken = default);

    /// <summary>Pauses playback. No-op if not playing.</summary>
    void Pause();

    /// <summary>Resumes paused playback. No-op if not paused.</summary>
    void Resume();

    /// <summary>Stops playback immediately. No-op if idle.</summary>
    void Stop();
}
