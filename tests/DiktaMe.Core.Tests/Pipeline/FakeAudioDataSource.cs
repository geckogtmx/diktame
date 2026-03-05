using DiktaMe.Core.Audio;

namespace DiktaMe.Core.Tests.Pipeline;

/// <summary>
/// Fake audio data source for testing <see cref="StreamingDictationPipeline"/>.
/// Allows tests to simulate audio chunks and recording stop events.
/// </summary>
internal sealed class FakeAudioDataSource : IAudioDataSource
{
    public event EventHandler<AudioDataAvailableEventArgs>? AudioDataAvailable;
    public event EventHandler<RecordingStoppedEventArgs>? RecordingStopped;
    public event EventHandler<RecordingStoppedEventArgs>? AutoStopped;

    /// <summary>Simulate an audio chunk arriving from the microphone.</summary>
    public void SimulateAudioChunk(byte[] pcm)
        => AudioDataAvailable?.Invoke(this, new AudioDataAvailableEventArgs(pcm, pcm.Length));

    /// <summary>Simulate manual recording stop.</summary>
    public void SimulateStop(long durationMs = 3000)
        => RecordingStopped?.Invoke(this, new RecordingStoppedEventArgs(
            "fake.wav", wasAutoStopped: false, durationMs));

    /// <summary>Simulate auto-stop (max duration reached).</summary>
    public void SimulateAutoStop(long durationMs = 60000)
        => AutoStopped?.Invoke(this, new RecordingStoppedEventArgs(
            "fake.wav", wasAutoStopped: true, durationMs));
}
