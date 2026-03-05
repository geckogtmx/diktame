
using DiktaMe.Core.STT;

namespace DiktaMe.Core.Config;
/// <summary>
/// Creates <see cref="ISTTProvider"/> instances based on a provider type name and API key.
/// Used by <see cref="PipelineFactory"/> to build mode-aware providers from settings.
/// </summary>
public interface ISTTProviderFactory
{
    /// <summary>
    /// Creates an STT provider for the given type.
    /// </summary>
    /// <param name="providerType">
    /// Provider type name: "deepgram", "gemini-audio", "whisper".
    /// </param>
    /// <param name="apiKey">API key (required for cloud providers; ignored for local).</param>
    ISTTProvider CreateProvider(string providerType, string? apiKey = null);

    /// <summary>
    /// Returns true if the given provider type supports real-time streaming.
    /// </summary>
    bool SupportsStreaming(string providerType);

    /// <summary>
    /// Creates a streaming STT provider, or returns null if the provider type
    /// does not support streaming.
    /// </summary>
    IStreamingSTTProvider? CreateStreamingProvider(string providerType, string? apiKey = null);
}
