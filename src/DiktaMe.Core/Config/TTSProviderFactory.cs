using System.Collections.Concurrent;
using System.Net.Http;
using DiktaMe.Core.TTS;
using Serilog;

namespace DiktaMe.Core.Config;

/// <summary>
/// Creates TTS providers by name, pulling API keys from <see cref="Security.SecureStorage"/>
/// and caching instances to reuse loaded models and HTTP connections.
/// Follows the <see cref="LLMProviderFactory"/> caching pattern with <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class TTSProviderFactory : ITTSProviderFactory, IDisposable
{
    private readonly Security.SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private readonly ConcurrentDictionary<string, ITTSProvider> _cache = new(StringComparer.Ordinal);

    public TTSProviderFactory(Security.SecureStorage secureStorage, SettingsManager settings)
    {
        _secureStorage = secureStorage;
        _settings = settings;
    }

    /// <inheritdoc/>
    public ITTSProvider CreateProvider(string providerType, string? modelVariant = null)
    {
        string type = providerType.ToLowerInvariant();

        // Stateless — no caching needed
        if (type is "none" or "skip")
        {
            return new NullTtsProvider();
        }

        string variant = modelVariant ?? ResolveVariant(type);
        string cacheKey = $"{type}:{variant}";

        bool isNew = false;
        var provider = _cache.GetOrAdd(cacheKey, _ =>
        {
            isNew = true;
            return CreateProviderCore(type, variant);
        });

        if (isNew)
        {
            Log.Information("TTSProviderFactory: created {CacheKey}", cacheKey);
        }
        else
        {
            Log.Debug("TTSProviderFactory: cache hit {CacheKey}", cacheKey);
        }

        return provider;
    }

    private string ResolveVariant(string type)
    {
        return type switch
        {
            "kokoro" => _settings.Current.Tts.KokoroModelVariant,
            "deepgram" => "aura-asteria-en",
            "inworld" => "inworld-tts-1.5-max",
            "openai" => "tts-1",
            _ => string.Empty,
        };
    }

    private ITTSProvider CreateProviderCore(string type, string variant)
    {
        return type switch
        {
            "kokoro" => new KokoroTtsProvider(
                modelVariant: variant,
                speed: (float)_settings.Current.Tts.Speed),

            "deepgram" => CreateCloudProvider(
                "deepgram",
                key => new DeepgramTtsProvider(key, model: variant)),

            "inworld" => CreateCloudProvider(
                "inworld",
                key => new InworldTtsProvider(key, model: variant)),

            "openai" => CreateCloudProvider(
                "openai",
                key => new OpenAITtsProvider(key, model: variant, speed: _settings.Current.Tts.Speed)),

            _ => throw new NotSupportedException($"Unknown TTS provider type: '{type}'."),
        };
    }

    private ITTSProvider CreateCloudProvider(string providerName, Func<string, ITTSProvider> factory)
    {
        string? key = _secureStorage.RetrieveKey(providerName);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException($"{providerName} API key not configured.");
        }

        return factory(key);
    }

    public void Dispose()
    {
        foreach (var provider in _cache.Values)
        {
            if (provider is IDisposable disposable)
            {
                disposable.Dispose();
            }
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
