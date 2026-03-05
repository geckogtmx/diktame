
using DiktaMe.Core.STT;

namespace DiktaMe.Core.Config;
/// <summary>
/// Creates STT providers by name, pulling API keys from <see cref="Security.SecureStorage"/>
/// and Deepgram settings from <see cref="SettingsManager"/>.
/// </summary>
public sealed class STTProviderFactory : ISTTProviderFactory
{
    private readonly Security.SecureStorage _secureStorage;
    private readonly SettingsManager _settings;

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

            "whisper" or "whisper-turbo" => new WhisperProvider("turbo"),
            "whisper-small" => new WhisperProvider("small"),
            "whisper-base" => new WhisperProvider("base"),
            "whisper-tiny" => new WhisperProvider("tiny"),
            "whisper-large" => new WhisperProvider("large"),

            _ => throw new NotSupportedException($"Unknown STT provider type: '{providerType}'."),
        };
    }
}
