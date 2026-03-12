using DiktaMe.Core.TTS;

namespace DiktaMe.Core.Config;

/// <summary>
/// Creates <see cref="ITTSProvider"/> instances based on provider type.
/// Used by <see cref="TTSRouter"/> and pipeline factories.
/// Follows the same pattern as <see cref="ILLMProviderFactory"/>.
/// </summary>
public interface ITTSProviderFactory
{
    /// <summary>
    /// Creates a TTS provider for the given type.
    /// </summary>
    /// <param name="providerType">
    /// Provider type name: "kokoro", "deepgram", "inworld", "openai", "none".
    /// </param>
    /// <param name="modelVariant">
    /// Model variant override for Kokoro: "int8", "fp16", "fp32".
    /// Ignored for cloud providers.
    /// </param>
    ITTSProvider CreateProvider(string providerType, string? modelVariant = null);
}
