using System.Diagnostics;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Local TTS provider using KokoroSharp (Kokoro-82M ONNX model).
/// Uses <see cref="KokoroModel"/> directly for inference, bypassing KokoroSharp's
/// built-in playback to return raw audio for <see cref="TtsPlayerService"/>.
/// Output: 24 kHz, 16-bit mono PCM.
/// </summary>
public sealed class KokoroTtsProvider : ITTSProvider
{
    /// <summary>Output sample rate of Kokoro models.</summary>
    public const int SampleRate = 24_000;

    /// <summary>Maximum tokens per inference call (KokoroModel hard limit).</summary>
    private const int MaxTokensPerSegment = 510;

    private readonly KokoroModelManager _modelManager;
    private readonly float _speed;
    private readonly object _lock = new();
    private KokoroModel? _model;
    private bool _voicesLoaded;
    private bool _disposed;

    public string ProviderName => "Kokoro-ONNX (Local)";
    public bool SupportsStreaming => false;

    /// <summary>Whether the ONNX model is loaded in memory and ready for inference.</summary>
    public bool IsModelLoaded => _model is not null;

    /// <summary>
    /// Creates a new Kokoro TTS provider.
    /// </summary>
    /// <param name="modelVariant">Model variant: "int8", "fp16", or "fp32".</param>
    /// <param name="speed">Speech speed multiplier (0.5–2.0).</param>
    public KokoroTtsProvider(string modelVariant = "int8", float speed = 1.0f)
    {
        _modelManager = new KokoroModelManager(modelVariant);
        _speed = Math.Clamp(speed, 0.5f, 2.0f);
    }

    public async Task<TtsResult> SynthesizeAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return EmptyResult();

        var sw = Stopwatch.StartNew();

        // Ensure model is loaded
        await EnsureModelLoadedAsync(cancellationToken);

        // Get voice
        var voice = GetVoice(voiceId ?? "af_bella");
        if (voice is null)
        {
            Log.Warning("KokoroTtsProvider: voice '{VoiceId}' not found, falling back to af_bella", voiceId);
            voice = GetVoice("af_bella");
            if (voice is null)
            {
                Log.Error("KokoroTtsProvider: no voices available");
                return EmptyResult();
            }
        }

        // Tokenize
        int[] tokens;
        try
        {
            tokens = Tokenizer.Tokenize(text, "en-us");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "KokoroTtsProvider: tokenization failed");
            return EmptyResult();
        }

        if (tokens.Length == 0)
            return EmptyResult();

        cancellationToken.ThrowIfCancellationRequested();

        // Synthesize (segment if needed for the 510-token limit)
        float[] audioSamples;
        try
        {
            audioSamples = SynthesizeSegmented(tokens, voice, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "KokoroTtsProvider: inference failed");
            return EmptyResult();
        }

        if (audioSamples.Length == 0)
            return EmptyResult();

        // Convert float[] (–1.0 to 1.0) → byte[] (16-bit PCM)
        byte[] pcmBytes = ConvertToPcm16(audioSamples);
        var duration = TimeSpan.FromSeconds((double)audioSamples.Length / SampleRate);

        return new TtsResult(
            AudioData: pcmBytes,
            Duration: duration,
            SampleRate: SampleRate,
            Provider: "kokoro-onnx",
            LatencyMs: sw.ElapsedMilliseconds,
            Format: "pcm");
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(_modelManager.IsModelDownloaded);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Gets the model manager for download operations.
    /// </summary>
    public KokoroModelManager ModelManager => _modelManager;

    // ── Private ────────────────────────────────────────────────────────────

    private async Task EnsureModelLoadedAsync(CancellationToken cancellationToken)
    {
        if (_model is not null)
            return;

        if (!_modelManager.IsModelDownloaded)
            throw new FileNotFoundException(
                "Kokoro TTS model not downloaded. Open Settings > Text-to-Speech to download it.");

        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (_model is not null) return;
                var loadSw = Stopwatch.StartNew();
                _model = new KokoroModel(_modelManager.ModelPath);

                if (!_voicesLoaded)
                {
                    KokoroVoiceManager.LoadVoicesFromPath();
                    _voicesLoaded = true;
                }

                Log.Information("KokoroTtsProvider: model loaded in {Ms}ms from {Path}",
                    loadSw.ElapsedMilliseconds, _modelManager.ModelPath);
            }
        }, cancellationToken);
    }

    private static KokoroVoice? GetVoice(string voiceId)
    {
        try
        {
            return KokoroVoiceManager.GetVoice(voiceId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Synthesizes audio, splitting into segments if tokens exceed the 510-token limit.
    /// </summary>
    private float[] SynthesizeSegmented(int[] tokens, KokoroVoice voice, CancellationToken cancellationToken)
    {
        if (tokens.Length <= MaxTokensPerSegment)
        {
            return _model!.Infer(tokens, voice, _speed);
        }

        // Split into segments at the token limit
        var allSamples = new List<float[]>();
        int offset = 0;

        while (offset < tokens.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int count = Math.Min(MaxTokensPerSegment, tokens.Length - offset);
            var segment = new int[count];
            Array.Copy(tokens, offset, segment, 0, count);

            var samples = _model!.Infer(segment, voice, _speed);
            if (samples.Length > 0)
                allSamples.Add(samples);

            offset += count;
        }

        // Concatenate all segments
        int totalLength = 0;
        foreach (var s in allSamples) totalLength += s.Length;

        var result = new float[totalLength];
        int pos = 0;
        foreach (var s in allSamples)
        {
            Array.Copy(s, 0, result, pos, s.Length);
            pos += s.Length;
        }

        return result;
    }

    /// <summary>
    /// Converts float PCM (–1.0 to 1.0) to 16-bit signed PCM bytes.
    /// </summary>
    internal static byte[] ConvertToPcm16(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            float clamped = Math.Clamp(samples[i], -1f, 1f);
            short value = (short)(clamped * short.MaxValue);
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 2), value);
        }
        return bytes;
    }

    private static TtsResult EmptyResult() => new(
        AudioData: [],
        Duration: TimeSpan.Zero,
        SampleRate: SampleRate,
        Provider: "kokoro-onnx",
        LatencyMs: 0,
        Format: "pcm");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_lock)
        {
            _model?.Dispose();
            _model = null;
        }
    }
}
