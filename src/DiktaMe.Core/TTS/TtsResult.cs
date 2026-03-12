namespace DiktaMe.Core.TTS;

/// <summary>
/// The result of a TTS synthesis operation.
/// Mirrors <see cref="LLM.LlmResult"/> pattern for consistency.
/// </summary>
/// <param name="AudioData">PCM or WAV audio bytes.</param>
/// <param name="Duration">Estimated audio duration.</param>
/// <param name="SampleRate">Sample rate in Hz (e.g. 22050, 24000, 44100).</param>
/// <param name="Provider">Provider identifier (e.g. "kokoro-onnx", "deepgram-aura-2").</param>
/// <param name="LatencyMs">Time-to-audio-complete in milliseconds.</param>
/// <param name="Format">Audio format: "wav" or "pcm".</param>
public sealed record TtsResult(
    byte[] AudioData,
    TimeSpan Duration,
    int SampleRate,
    string Provider,
    long LatencyMs,
    string Format)
{
    /// <summary>Whether the result contains valid audio data.</summary>
    public bool IsSuccess => AudioData.Length > 0;
}
