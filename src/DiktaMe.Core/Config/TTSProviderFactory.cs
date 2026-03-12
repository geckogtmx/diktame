using System.Collections.Concurrent;
using DiktaMe.Core.TTS;

namespace DiktaMe.Core.Config;

/// <summary>
/// Creates TTS providers by name, caching instances to reuse loaded models and HTTP connections.
/// Follows the <see cref="LLMProviderFactory"/> caching pattern with <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class TTSProviderFactory : ITTSProviderFactory, IDisposable
{
    private readonly SettingsManager _settings;
    private readonly ConcurrentDictionary<string, ITTSProvider> _cache = new(StringComparer.Ordinal);

    public TTSProviderFactory(SettingsManager settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public ITTSProvider CreateProvider(string providerType, string? modelVariant = null)
    {
        string type = providerType.ToLowerInvariant();

        // Stateless — no caching needed
        if (type is "none" or "skip")
            return new NullTtsProvider();

        string variant = modelVariant ?? _settings.Current.Tts.KokoroModelVariant;
        string cacheKey = $"{type}:{variant}";

        return _cache.GetOrAdd(cacheKey, _ => CreateProviderCore(type, variant));
    }

    private ITTSProvider CreateProviderCore(string type, string variant)
    {
        return type switch
        {
            "kokoro" => new KokoroTtsProvider(
                modelVariant: variant,
                speed: (float)_settings.Current.Tts.Speed),

            // Cloud providers will be added in Phase E:
            // "deepgram" => new DeepgramTtsProvider(...),
            // "inworld" => new InworldTtsProvider(...),
            // "openai" => new OpenAITtsProvider(...),

            _ => throw new NotSupportedException($"Unknown TTS provider type: '{type}'."),
        };
    }

    public void Dispose()
    {
        foreach (var provider in _cache.Values)
        {
            if (provider is IDisposable disposable)
                disposable.Dispose();
        }
        _cache.Clear();
    }
}

/// <summary>
/// A no-op TTS provider that returns empty audio.
/// Used when TTS is disabled or set to "none".
/// </summary>
internal sealed class NullTtsProvider : ITTSProvider
{
    public string ProviderName => "None";
    public bool SupportsStreaming => false;

    public Task<TtsResult> SynthesizeAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TtsResult(
            AudioData: [],
            Duration: TimeSpan.Zero,
            SampleRate: 24_000,
            Provider: ProviderName,
            LatencyMs: 0,
            Format: "pcm"));

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public void Dispose() { }
}
