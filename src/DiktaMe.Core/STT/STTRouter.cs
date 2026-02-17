namespace DiktaMe.Core.STT;

using Serilog;

/// <summary>
/// Routes transcription requests to the correct <see cref="ISTTProvider"/> based on
/// the configured primary provider, with automatic fallback to a secondary provider
/// if the primary fails or is unavailable.
/// Port of the provider-selection logic in V1's transcriber.py / pipelines.py.
/// </summary>
public sealed class STTRouter : ISTTProvider
{
    private readonly ISTTProvider _primary;
    private readonly ISTTProvider? _fallback;

    /// <inheritdoc/>
    public string ProviderName =>
        _fallback is null
            ? _primary.ProviderName
            : $"{_primary.ProviderName} (fallback: {_fallback.ProviderName})";

    /// <summary>
    /// Initialises the router with a primary provider and an optional fallback.
    /// </summary>
    /// <param name="primary">The preferred provider to use first.</param>
    /// <param name="fallback">
    /// Optional secondary provider used when the primary is unavailable or throws.
    /// </param>
    public STTRouter(ISTTProvider primary, ISTTProvider? fallback = null)
    {
        _primary = primary;
        _fallback = fallback;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (await _primary.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
            return true;

        if (_fallback is not null)
            return await _fallback.IsAvailableAsync(cancellationToken).ConfigureAwait(false);

        return false;
    }

    /// <summary>
    /// Transcribes the audio file using the primary provider.
    /// Falls back to the secondary provider if the primary throws or returns an empty result.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeAsync(
        string audioFilePath,
        string language = "en",
        CancellationToken cancellationToken = default)
    {
        // ── Try primary ───────────────────────────────────────────────────────
        try
        {
            var result = await _primary.TranscribeAsync(audioFilePath, language, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                Log.Debug("STTRouter: primary {Provider} succeeded", _primary.ProviderName);
                return result;
            }

            Log.Warning("STTRouter: primary {Provider} returned empty result — trying fallback",
                _primary.ProviderName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "STTRouter: primary {Provider} threw — trying fallback",
                _primary.ProviderName);
        }

        // ── Fallback ──────────────────────────────────────────────────────────
        if (_fallback is not null)
        {
            try
            {
                var fallbackResult = await _fallback.TranscribeAsync(audioFilePath, language, cancellationToken)
                    .ConfigureAwait(false);

                Log.Information("STTRouter: fallback {Provider} result: success={Success}",
                    _fallback.ProviderName, fallbackResult.IsSuccess);

                return fallbackResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "STTRouter: fallback {Provider} also threw", _fallback.ProviderName);
            }
        }

        // ── Both failed ───────────────────────────────────────────────────────
        Log.Error("STTRouter: all providers failed for {File}", audioFilePath);
        return new TranscriptionResult
        {
            Text = string.Empty,
            Provider = ProviderName,
            LatencyMs = 0,
        };
    }

    /// <summary>
    /// Returns which providers are currently available.
    /// </summary>
    public async Task<ProviderAvailability> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        bool primaryAvailable = await _primary.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        bool fallbackAvailable = _fallback is not null
            && await _fallback.IsAvailableAsync(cancellationToken).ConfigureAwait(false);

        return new ProviderAvailability(
            _primary.ProviderName, primaryAvailable,
            _fallback?.ProviderName, fallbackAvailable);
    }
}

/// <summary>
/// Describes which STT providers are currently available and configured.
/// </summary>
public sealed record ProviderAvailability(
    string PrimaryName,
    bool PrimaryAvailable,
    string? FallbackName,
    bool FallbackAvailable)
{
    /// <summary>Name of the best currently-available provider.</summary>
    public string? BestAvailable =>
        PrimaryAvailable ? PrimaryName :
        FallbackAvailable ? FallbackName :
        null;
}
