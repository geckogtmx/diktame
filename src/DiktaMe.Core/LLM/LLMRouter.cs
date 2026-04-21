
using DiktaMe.Core.Config;  // ILLMProviderFactory
using Serilog;

namespace DiktaMe.Core.LLM;
/// <summary>
/// Routes LLM processing requests to the correct <see cref="ILLMProvider"/>
/// based on a configured primary provider, with automatic fallback.
/// Supports per-mode model selection by dynamically resolving providers from model names.
/// When <see cref="AuthMode.Wallet"/> is active, routes dictation/rewrite to the wallet proxy
/// but skips wallet for chat (BYOK/local only).
/// Implements <see cref="ILLMProvider"/> itself so callers are provider-agnostic.
/// Mirrors <c>STTRouter</c> in the STT layer.
/// </summary>
public sealed class LLMRouter : ILLMProvider
{
    private readonly ILLMProvider _primary;
    private readonly ILLMProvider? _fallback;
    private readonly ILLMProviderFactory _factory;
    private readonly SettingsManager? _settings;
    private readonly ILLMProvider? _walletProvider;

    /// <inheritdoc/>
    public string ProviderName =>
        _fallback is null
            ? _primary.ProviderName
            : $"{_primary.ProviderName} (fallback: {_fallback.ProviderName})";

    /// <param name="primary">The preferred provider (used when no model override is specified).</param>
    /// <param name="factory">Factory for creating providers dynamically based on model names. API keys are resolved internally by the factory.</param>
    /// <param name="fallback">Optional secondary provider used when primary fails.</param>
    /// <param name="settings">Optional settings manager for AuthMode-aware routing.</param>
    /// <param name="walletProvider">Optional wallet LLM proxy for managed Pay-As-You-Go routing.</param>
    public LLMRouter(
        ILLMProvider primary,
        ILLMProviderFactory factory,
        ILLMProvider? fallback = null,
        SettingsManager? settings = null,
        ILLMProvider? walletProvider = null)
    {
        _primary = primary;
        _fallback = fallback;
        _factory = factory;
        _settings = settings;
        _walletProvider = walletProvider;
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (await _primary.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (_fallback is not null)
        {
            return await _fallback.IsAvailableAsync(cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Falls back to the secondary provider when the primary throws or returns empty output.
    /// Returns an empty <see cref="LlmResult"/> (IsSuccess = false) when all providers fail.
    /// </remarks>
    public Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
    {
        // Delegate to overload with no model override
        return ProcessAsync(text, systemPrompt, modelName: null, mode, cancellationToken);
    }

    /// <summary>
    /// Processes text through the LLM with optional per-request model override.
    /// </summary>
    /// <param name="text">The input text to process.</param>
    /// <param name="systemPrompt">The system prompt for the LLM.</param>
    /// <param name="modelName">Optional model override (e.g., "gpt-4o-mini", "claude-sonnet-4"). When null, uses the primary provider.</param>
    /// <param name="mode">The pipeline mode (e.g., "dictate", "refine").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The LLM result.</returns>
    public async Task<LlmResult> ProcessAsync(
        string text,
        string systemPrompt,
        string? modelName,
        string mode = "dictate",
        CancellationToken cancellationToken = default)
    {
        // Wallet mode — route through managed wallet proxy (dictation/rewrite only)
        if (_settings?.Current.AuthMode == AuthMode.Wallet && _walletProvider is not null)
        {
            Log.Debug("LLMRouter: AuthMode=Wallet — routing to {Provider}", _walletProvider.ProviderName);
            return await _walletProvider.ProcessAsync(text, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);
        }

        // If no model override, use the primary provider
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return await ProcessWithProviderAsync(_primary, _fallback, text, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);
        }

        // Resolve provider from model name
        ILLMProvider? provider;
        try
        {
            provider = ResolveProviderForModel(modelName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LLMRouter: Failed to resolve provider for model '{ModelName}'", modelName);
            return new LlmResult
            {
                Text = string.Empty,
                Provider = "unknown",
                LatencyMs = 0,
                ErrorMessage = $"LLM provider for '{modelName}' unavailable — check API keys in Settings.",
            };
        }

        // Use resolved provider (no fallback for per-mode model selection — fail fast if model is unavailable)
        return await ProcessWithProviderAsync(provider, fallback: null, text, systemPrompt, mode, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode = "chat",
        CancellationToken cancellationToken = default)
    {
        return ProcessConversationAsync(history, systemPrompt, modelName: null, mode, cancellationToken);
    }

    /// <summary>
    /// Processes a multi-turn conversation with optional per-request model override.
    /// </summary>
    /// <param name="history">Ordered conversation turns.</param>
    /// <param name="systemPrompt">The system prompt for the LLM.</param>
    /// <param name="modelName">Optional model override. When null, uses the primary provider.</param>
    /// <param name="mode">The pipeline mode (e.g., "chat").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The LLM result.</returns>
    public async Task<LlmResult> ProcessConversationAsync(
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string? modelName,
        string mode = "chat",
        CancellationToken cancellationToken = default)
    {
        // Wallet mode — chat is excluded from wallet credits (BYOK/local only).
        // Fall through to primary/fallback providers instead.
        if (_settings?.Current.AuthMode == AuthMode.Wallet)
        {
            Log.Debug("LLMRouter: AuthMode=Wallet — chat excluded from wallet, using BYOK/local providers");
        }

        // If no model override, use the primary provider
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return await ProcessConversationWithProviderAsync(
                _primary, _fallback, history, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);
        }

        // Resolve provider from model name
        ILLMProvider? provider;
        try
        {
            provider = ResolveProviderForModel(modelName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LLMRouter: Failed to resolve provider for model '{ModelName}'", modelName);
            return new LlmResult
            {
                Text = string.Empty,
                Provider = "unknown",
                LatencyMs = 0,
                ErrorMessage = $"LLM provider for '{modelName}' unavailable — check API keys in Settings.",
            };
        }

        // Use resolved provider (no fallback for per-mode model selection)
        return await ProcessConversationWithProviderAsync(
            provider, fallback: null, history, systemPrompt, mode, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Processes an image through the LLM (no model override).
    /// </summary>
    public Task<LlmResult> ProcessWithImageAsync(
        byte[] imageData, string mimeType,
        string text, string systemPrompt, string mode = "vision",
        CancellationToken cancellationToken = default)
    {
        return ProcessWithImageAsync(imageData, mimeType, text, systemPrompt, modelName: null, mode, cancellationToken);
    }

    /// <summary>
    /// Processes an image through the LLM with optional per-request model override.
    /// </summary>
    public async Task<LlmResult> ProcessWithImageAsync(
        byte[] imageData, string mimeType,
        string text, string systemPrompt, string? modelName,
        string mode = "vision",
        CancellationToken cancellationToken = default)
    {
        // Wallet mode — route through managed wallet proxy
        if (_settings?.Current.AuthMode == AuthMode.Wallet && _walletProvider is not null)
        {
            Log.Debug("LLMRouter: AuthMode=Wallet — routing image to {Provider}", _walletProvider.ProviderName);
            return await _walletProvider.ProcessWithImageAsync(imageData, mimeType, text, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(modelName))
        {
            return await ProcessImageWithProviderAsync(
                _primary, _fallback, imageData, mimeType, text, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);
        }

        ILLMProvider? provider;
        try
        {
            provider = ResolveProviderForModel(modelName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LLMRouter: Failed to resolve provider for model '{ModelName}'", modelName);
            return new LlmResult
            {
                Text = string.Empty,
                Provider = "unknown",
                LatencyMs = 0,
                ErrorMessage = $"Vision provider for '{modelName}' unavailable — check API keys in Settings.",
            };
        }

        return await ProcessImageWithProviderAsync(
            provider, fallback: null, imageData, mimeType, text, systemPrompt, mode, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves the appropriate LLM provider for a given model name.
    /// Uses <see cref="ModelListService.ResolveProviderFromModelId"/> for prefix matching,
    /// then <see cref="LLMProviderFactory.CreateProvider"/> to instantiate the provider.
    /// </summary>
    private ILLMProvider ResolveProviderForModel(string modelName)
    {
        string providerType = ModelListService.ResolveProviderFromModelId(modelName);
        Log.Debug("LLMRouter: Resolved model '{ModelName}' to provider '{ProviderType}'", modelName, providerType);

        // CreateProvider pulls API keys from SecureStorage internally
        return _factory.CreateProvider(providerType, model: modelName);
    }

    /// <summary>
    /// Applies per-request settings to the provider (e.g. Gemini web search grounding).
    /// </summary>
    private void ApplyProviderSettings(ILLMProvider provider)
    {
        if (provider is GeminiProvider gemini && _settings is not null)
        {
            gemini.WebSearchEnabled = _settings.Current.Chat.WebSearchEnabled;
        }
    }

    /// <summary>
    /// Executes the LLM processing with fallback support.
    /// </summary>
    private async Task<LlmResult> ProcessWithProviderAsync(
        ILLMProvider primary,
        ILLMProvider? fallback,
        string text,
        string systemPrompt,
        string mode,
        CancellationToken cancellationToken)
    {
        // ── Try primary ───────────────────────────────────────────────────────
        try
        {
            ApplyProviderSettings(primary);
            var result = await primary.ProcessAsync(text, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                Log.Debug("LLMRouter: primary {Provider} succeeded", primary.ProviderName);
                return result;
            }

            Log.Warning("LLMRouter: primary {Provider} returned empty result — trying fallback",
                primary.ProviderName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "LLMRouter: primary {Provider} threw — trying fallback",
                primary.ProviderName);
        }

        // ── Fallback ──────────────────────────────────────────────────────────
        if (fallback is not null)
        {
            try
            {
                ApplyProviderSettings(fallback);
                var fallbackResult = await fallback.ProcessAsync(text, systemPrompt, mode, cancellationToken)
                    .ConfigureAwait(false);

                Log.Information("LLMRouter: fallback {Provider} result: success={Success}",
                    fallback.ProviderName, fallbackResult.IsSuccess);

                return fallbackResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LLMRouter: fallback {Provider} also threw", fallback.ProviderName);
            }
        }

        // ── Both failed ───────────────────────────────────────────────────────
        Log.Error("LLMRouter: all providers failed");
        return new LlmResult
        {
            Text = string.Empty,
            Provider = primary.ProviderName,
            LatencyMs = 0,
            ErrorMessage = $"LLM request failed ({primary.ProviderName}) — check API keys and connectivity.",
        };
    }

    /// <summary>
    /// Executes the conversation processing with fallback support.
    /// </summary>
    private async Task<LlmResult> ProcessConversationWithProviderAsync(
        ILLMProvider primary,
        ILLMProvider? fallback,
        IReadOnlyList<ConversationTurn> history,
        string systemPrompt,
        string mode,
        CancellationToken cancellationToken)
    {
        // ── Try primary ───────────────────────────────────────────────────────
        try
        {
            ApplyProviderSettings(primary);
            var result = await primary.ProcessConversationAsync(history, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                Log.Debug("LLMRouter: primary {Provider} conversation succeeded", primary.ProviderName);
                return result;
            }

            Log.Warning("LLMRouter: primary {Provider} conversation returned empty — trying fallback",
                primary.ProviderName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "LLMRouter: primary {Provider} conversation threw — trying fallback",
                primary.ProviderName);
        }

        // ── Fallback ──────────────────────────────────────────────────────────
        if (fallback is not null)
        {
            try
            {
                ApplyProviderSettings(fallback);
                var fallbackResult = await fallback.ProcessConversationAsync(
                    history, systemPrompt, mode, cancellationToken)
                    .ConfigureAwait(false);

                Log.Information("LLMRouter: fallback {Provider} conversation result: success={Success}",
                    fallback.ProviderName, fallbackResult.IsSuccess);

                return fallbackResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LLMRouter: fallback {Provider} conversation also threw",
                    fallback.ProviderName);
            }
        }

        // ── Both failed ───────────────────────────────────────────────────────
        Log.Error("LLMRouter: all providers failed for conversation");
        return new LlmResult
        {
            Text = string.Empty,
            Provider = primary.ProviderName,
            LatencyMs = 0,
            ErrorMessage = $"Chat request failed ({primary.ProviderName}) — check API keys and connectivity.",
        };
    }

    /// <summary>
    /// Executes the image processing with fallback support.
    /// </summary>
    private async Task<LlmResult> ProcessImageWithProviderAsync(
        ILLMProvider primary,
        ILLMProvider? fallback,
        byte[] imageData,
        string mimeType,
        string text,
        string systemPrompt,
        string mode,
        CancellationToken cancellationToken)
    {
        // ── Try primary ───────────────────────────────────────────────────────
        try
        {
            ApplyProviderSettings(primary);
            var result = await primary.ProcessWithImageAsync(imageData, mimeType, text, systemPrompt, mode, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                Log.Debug("LLMRouter: primary {Provider} image succeeded", primary.ProviderName);
                return result;
            }

            Log.Warning("LLMRouter: primary {Provider} image returned empty — trying fallback",
                primary.ProviderName);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "LLMRouter: primary {Provider} image threw — trying fallback",
                primary.ProviderName);
        }

        // ── Fallback ──────────────────────────────────────────────────────────
        if (fallback is not null)
        {
            try
            {
                ApplyProviderSettings(fallback);
                var fallbackResult = await fallback.ProcessWithImageAsync(
                    imageData, mimeType, text, systemPrompt, mode, cancellationToken)
                    .ConfigureAwait(false);

                Log.Information("LLMRouter: fallback {Provider} image result: success={Success}",
                    fallback.ProviderName, fallbackResult.IsSuccess);

                return fallbackResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LLMRouter: fallback {Provider} image also threw",
                    fallback.ProviderName);
            }
        }

        // ── Both failed ───────────────────────────────────────────────────────
        Log.Error("LLMRouter: all providers failed for image processing");
        return new LlmResult
        {
            Text = string.Empty,
            Provider = primary.ProviderName,
            LatencyMs = 0,
            ErrorMessage = $"Vision request failed ({primary.ProviderName}) — check API keys and connectivity.",
        };
    }
}
