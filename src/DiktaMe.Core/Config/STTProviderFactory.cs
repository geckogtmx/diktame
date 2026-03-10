
using DiktaMe.Core.STT;

namespace DiktaMe.Core.Config;
/// <summary>
/// Creates STT providers by name, pulling API keys from <see cref="Security.SecureStorage"/>
/// and Deepgram settings from <see cref="SettingsManager"/>.
/// Caches <see cref="WhisperProvider"/> instances to avoid reloading the ~466MB GGML model
/// on every dictation (~800ms overhead per call without caching).
/// </summary>
public sealed class STTProviderFactory : ISTTProviderFactory, IDisposable
{
    private readonly Security.SecureStorage _secureStorage;
    private readonly SettingsManager _settings;
    private WhisperProvider? _cachedWhisper;
    private string? _cachedWhisperModel;

    public STTProviderFactory(Security.SecureStorage secureStorage, SettingsManager settings)
    {
        _secureStorage = secureStorage;
        _settings = settings;
    }

    /// <inheritdoc/>
    public ISTTProvider CreateProvider(string providerType, string? apiKey = null)
    {
        // If no key was passed, try loading from secure storage
        string? key = apiKey
            ?? _secureStorage.RetrieveKey(providerType.ToLowerInvariant());

        return providerType.ToLowerInvariant() switch
        {
            "deepgram" => new DeepgramProvider(
                key ?? throw new InvalidOperationException("Deepgram API key not configured."),
                _settings.Current.Deepgram),

            "gemini-audio" => new GeminiAudioProvider(
                key ?? throw new InvalidOperationException("Gemini API key not configured.")),

            "whisper" => GetOrCreateWhisper(_settings.Current.WhisperModel),
            "whisper-turbo" => GetOrCreateWhisper("turbo"),
            "whisper-small" => GetOrCreateWhisper("small"),
            "whisper-base" => GetOrCreateWhisper("base"),
            "whisper-tiny" => GetOrCreateWhisper("tiny"),
            "whisper-large" => GetOrCreateWhisper("large"),

            _ => throw new NotSupportedException($"Unknown STT provider type: '{providerType}'."),
        };
    }

    /// <inheritdoc/>
    public bool SupportsStreaming(string providerType)
        => string.Equals(providerType, "deepgram", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IStreamingSTTProvider? CreateStreamingProvider(string providerType, string? apiKey = null)
    {
        string? key = apiKey
            ?? _secureStorage.RetrieveKey(providerType.ToLowerInvariant());

        return providerType.ToLowerInvariant() switch
        {
            "deepgram" => new DeepgramStreamingProvider(
                key ?? throw new InvalidOperationException("Deepgram API key not configured."),
                _settings.Current.Deepgram),

            _ => null, // Only Deepgram supports streaming
        };
    }

    /// <summary>
    /// Returns the cached <see cref="WhisperProvider"/> if the model size matches,
    /// otherwise disposes the old one and creates a new instance.
    /// </summary>
    private WhisperProvider GetOrCreateWhisper(string modelSize)
    {
        if (_cachedWhisper is not null
            && string.Equals(_cachedWhisperModel, modelSize, StringComparison.Ordinal))
        {
            return _cachedWhisper;
        }

        _cachedWhisper?.Dispose();
        _cachedWhisper = new WhisperProvider(modelSize);
        _cachedWhisperModel = modelSize;
        return _cachedWhisper;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cachedWhisper?.Dispose();
        _cachedWhisper = null;
    }
}
