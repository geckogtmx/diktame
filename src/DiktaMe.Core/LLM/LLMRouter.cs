namespace DiktaMe.Core.LLM;

using Serilog;

/// <summary>
/// Routes LLM processing requests to the correct <see cref="ILLMProvider"/>
/// based on a configured primary provider, with automatic fallback.
/// Implements <see cref="ILLMProvider"/> itself so callers are provider-agnostic.
/// Mirrors <c>STTRouter</c> in the STT layer.
/// </summary>
public sealed class LLMRouter : ILLMProvider
{
    private readonly ILLMProvider _primary;
    private readonly ILLMProvider? _fallback;

    /// <inheritdoc/>
    public string ProviderName =>
        _fallback is null
            ? _primary.ProviderName
            : $"{_primary.ProviderName} (fallback: {_fallback.ProviderName})";

    /// <param name="primary">The preferred provider.</param>
    /// <param name="fallback">Optional secondary provider used when primary fails.</param>
    public LLMRouter(ILLMProvider primary, ILLMProvider? fallback = null)
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

    /// <inheritdoc/>
    /// <remarks>
    /// Falls back to the secondary provider when the primary throws or returns empty output.
    /// Returns an empty <see cref="LlmResult"/> (IsSuccess = false) when all providers fail.
    /// </remarks>
    public async Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
    {
        // ── Try primary ───────────────────────────────────────────────────────
        try
        {
            var result = await _primary.ProcessAsync(text, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                Log.Debug("LLMRouter: primary {Provider} succeeded", _primary.ProviderName);
                return result;
            }

            Log.Warning("LLMRouter: primary {Provider} returned empty result — trying fallback",
                _primary.ProviderName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "LLMRouter: primary {Provider} threw — trying fallback",
                _primary.ProviderName);
        }

        // ── Fallback ──────────────────────────────────────────────────────────
        if (_fallback is not null)
        {
            try
            {
                var fallbackResult = await _fallback.ProcessAsync(text, systemPrompt, mode, cancellationToken)
                    .ConfigureAwait(false);

                Log.Information("LLMRouter: fallback {Provider} result: success={Success}",
                    _fallback.ProviderName, fallbackResult.IsSuccess);

                return fallbackResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LLMRouter: fallback {Provider} also threw", _fallback.ProviderName);
            }
        }

        // ── Both failed ───────────────────────────────────────────────────────
        Log.Error("LLMRouter: all providers failed");
        return new LlmResult
        {
            Text = string.Empty,
            Provider = ProviderName,
            LatencyMs = 0,
        };
    }
}
