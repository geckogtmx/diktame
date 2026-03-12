using Serilog;

namespace DiktaMe.Core.TTS;

/// <summary>
/// Routes TTS synthesis requests to a primary provider with optional fallback.
/// Follows the same pattern as <see cref="LLM.LLMRouter"/> and <see cref="STT.STTRouter"/>.
/// </summary>
public sealed class TTSRouter : ITTSProvider
{
    private readonly ITTSProvider _primary;
    private readonly ITTSProvider? _fallback;

    public string ProviderName => _fallback is not null
        ? $"{_primary.ProviderName} (fallback: {_fallback.ProviderName})"
        : _primary.ProviderName;

    public bool SupportsStreaming => _primary.SupportsStreaming;

    /// <summary>
    /// Creates a TTS router with a primary provider and optional fallback.
    /// </summary>
    /// <param name="primary">Primary TTS provider (tried first).</param>
    /// <param name="fallback">Fallback provider (tried if primary fails). Null = no fallback.</param>
    public TTSRouter(ITTSProvider primary, ITTSProvider? fallback = null)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<TtsResult> SynthesizeAsync(
        string text,
        string? voiceId = null,
        CancellationToken cancellationToken = default)
    {
        // Try primary
        try
        {
            var result = await _primary.SynthesizeAsync(text, voiceId, cancellationToken);
            if (result.IsSuccess)
            {
                Log.Debug("TTSRouter: synthesis succeeded via {Provider} ({Ms}ms)",
                    result.Provider, result.LatencyMs);
                return result;
            }

            Log.Warning("TTSRouter: primary provider {Provider} returned empty audio", _primary.ProviderName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TTSRouter: primary provider {Provider} failed", _primary.ProviderName);
        }

        // Try fallback
        if (_fallback is not null)
        {
            try
            {
                var result = await _fallback.SynthesizeAsync(text, voiceId, cancellationToken);
                if (result.IsSuccess)
                {
                    Log.Information("TTSRouter: fallback {Provider} succeeded ({Ms}ms)",
                        result.Provider, result.LatencyMs);
                    return result;
                }

                Log.Warning("TTSRouter: fallback provider {Provider} returned empty audio", _fallback.ProviderName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TTSRouter: fallback provider {Provider} also failed", _fallback.ProviderName);
            }
        }

        // Both failed — return empty result
        Log.Error("TTSRouter: all providers failed for text ({Length} chars)", text.Length);
        return new TtsResult(
            AudioData: [],
            Duration: TimeSpan.Zero,
            SampleRate: 24_000,
            Provider: "none",
            LatencyMs: 0,
            Format: "pcm");
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (await _primary.IsAvailableAsync(cancellationToken))
            return true;

        if (_fallback is not null)
            return await _fallback.IsAvailableAsync(cancellationToken);

        return false;
    }

    public void Dispose()
    {
        _primary.Dispose();
        _fallback?.Dispose();
    }
}
