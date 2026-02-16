using DiktaMe.Core.Audio;

namespace DiktaMe.Core.Tests.Audio;

/// <summary>
/// Tests for AudioRecorder state machine, event contracts, and file output.
/// Hardware-dependent recording tests are skipped on headless CI (no audio device).
/// </summary>
public class AudioRecorderTests : IDisposable
{
    private readonly AudioRecorder _recorder = new();

    public void Dispose() => _recorder.Dispose();

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void IsRecording_Initially_IsFalse()
    {
        Assert.False(_recorder.IsRecording);
    }

    // ── StopRecordingAsync when idle ──────────────────────────────────────────

    [Fact]
    public async Task StopRecordingAsync_WhenNotRecording_ReturnsNull()
    {
        // Act
        string? result = await _recorder.StopRecordingAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StopRecordingAsync_WhenNotRecording_DoesNotChangeIsRecording()
    {
        // Act
        await _recorder.StopRecordingAsync();

        // Assert
        Assert.False(_recorder.IsRecording);
    }

    // ── Double-start guard ────────────────────────────────────────────────────

    [Fact]
    public void StartRecording_WhenAlreadyRecording_ThrowsInvalidOperationException()
    {
        // Only runs if there is at least one audio device
        if (AudioDeviceManager.GetInputDevices().Count == 0)
            return;

        _recorder.StartRecording();

        try
        {
            // Act + Assert
            Assert.Throws<InvalidOperationException>(() => _recorder.StartRecording());
        }
        finally
        {
            _ = _recorder.StopRecordingAsync();
        }
    }

    // ── Disposed guard ────────────────────────────────────────────────────────

    [Fact]
    public void StartRecording_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _recorder.Dispose();

        // Act + Assert
        Assert.Throws<ObjectDisposedException>(() => _recorder.StartRecording());
    }

    // ── Recording lifecycle (requires audio device) ───────────────────────────

    [Fact]
    public async Task StartThenStop_SetsIsRecordingCorrectly()
    {
        if (AudioDeviceManager.GetInputDevices().Count == 0)
            return;

        // Act
        _recorder.StartRecording();
        Assert.True(_recorder.IsRecording);

        string? path = await _recorder.StopRecordingAsync();

        // Assert
        Assert.False(_recorder.IsRecording);
        Assert.NotNull(path);
    }

    [Fact]
    public async Task StartThenStop_RaisesRecordingStartedAndStopped()
    {
        if (AudioDeviceManager.GetInputDevices().Count == 0)
            return;

        bool startedFired = false;
        bool stoppedFired = false;
        _recorder.RecordingStarted += (_, _) => startedFired = true;
        _recorder.RecordingStopped += (_, _) => stoppedFired = true;

        _recorder.StartRecording();
        await Task.Delay(100);
        await _recorder.StopRecordingAsync();

        // Give the WaveIn stopped callback a moment to fire
        await Task.Delay(200);

        Assert.True(startedFired);
        Assert.True(stoppedFired);
    }

    [Fact]
    public async Task StartThenStop_ProducesValidWavFile()
    {
        if (AudioDeviceManager.GetInputDevices().Count == 0)
            return;

        _recorder.StartRecording();
        await Task.Delay(300); // record ~300 ms
        string? path = await _recorder.StopRecordingAsync();
        await Task.Delay(200); // let WaveIn finish flushing

        Assert.NotNull(path);
        Assert.True(File.Exists(path), $"WAV file not found: {path}");

        // Verify WAV header: RIFF signature, sample rate, bit depth, channels
        byte[] bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length >= 44, "WAV file too small to contain header");

        // RIFF chunk descriptor
        Assert.Equal((byte)'R', bytes[0]);
        Assert.Equal((byte)'I', bytes[1]);
        Assert.Equal((byte)'F', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);

        // WAVE format marker at offset 8
        Assert.Equal((byte)'W', bytes[8]);
        Assert.Equal((byte)'A', bytes[9]);
        Assert.Equal((byte)'V', bytes[10]);
        Assert.Equal((byte)'E', bytes[11]);

        // NumChannels (offset 22-23): should be 1 (mono)
        short numChannels = BitConverter.ToInt16(bytes, 22);
        Assert.Equal(1, numChannels);

        // SampleRate (offset 24-27): should be 16000
        int sampleRate = BitConverter.ToInt32(bytes, 24);
        Assert.Equal(16_000, sampleRate);

        // BitsPerSample (offset 34-35): should be 16
        short bitsPerSample = BitConverter.ToInt16(bytes, 34);
        Assert.Equal(16, bitsPerSample);

        // Cleanup
        File.Delete(path);
    }

    [Fact]
    public async Task RecordingStartedEvent_IncludesFilePath()
    {
        if (AudioDeviceManager.GetInputDevices().Count == 0)
            return;

        string? capturedPath = null;
        _recorder.RecordingStarted += (_, e) => capturedPath = e.FilePath;

        _recorder.StartRecording();
        await _recorder.StopRecordingAsync();

        Assert.NotNull(capturedPath);
        Assert.EndsWith(".wav", capturedPath, StringComparison.OrdinalIgnoreCase);
    }

    // ── Auto-stop ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AutoStop_FiresAutoStoppedEvent_WhenDurationExceeded()
    {
        if (AudioDeviceManager.GetInputDevices().Count == 0)
            return;

        var autoStopFired = new TaskCompletionSource<RecordingStoppedEventArgs>();
        _recorder.AutoStopped += (_, e) => autoStopFired.TrySetResult(e);

        // Record for max 1 second
        _recorder.StartRecording(maxDurationSeconds: 1);

        // Wait up to 3 seconds for the auto-stop event
        RecordingStoppedEventArgs args = await autoStopFired.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.True(args.WasAutoStopped);
        Assert.NotNull(args.FilePath);
        Assert.False(_recorder.IsRecording);

        // Give the WaveIn callback time to flush and close the file before we check it
        await Task.Delay(300);
        Assert.True(File.Exists(args.FilePath), $"Expected WAV file at: {args.FilePath}");
    }

    [Fact]
    public async Task AutoStop_DoesNotFireRecordingStopped()
    {
        if (AudioDeviceManager.GetInputDevices().Count == 0)
            return;

        bool stoppedFired = false;
        _recorder.RecordingStopped += (_, _) => stoppedFired = true;

        var autoStopFired = new TaskCompletionSource();
        _recorder.AutoStopped += (_, _) => autoStopFired.TrySetResult();

        _recorder.StartRecording(maxDurationSeconds: 1);
        await autoStopFired.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(200); // let any late events fire

        Assert.False(stoppedFired);
    }
}
