namespace DiktaMe.Core.Audio;

/// <summary>
/// Provides audio data events and stop notification for streaming pipelines.
/// Abstracts <see cref="AudioRecorder"/> for testability.
/// </summary>
public interface IAudioDataSource
{
    /// <summary>Raised for each chunk of raw PCM audio data during recording.</summary>
    event EventHandler<AudioDataAvailableEventArgs>? AudioDataAvailable;

    /// <summary>Raised when recording stops normally.</summary>
    event EventHandler<RecordingStoppedEventArgs>? RecordingStopped;

    /// <summary>Raised when recording stops automatically (max duration reached).</summary>
    event EventHandler<RecordingStoppedEventArgs>? AutoStopped;
}
