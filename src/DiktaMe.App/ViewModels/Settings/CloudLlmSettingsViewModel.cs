
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.LLM;
using Microsoft.UI.Dispatching;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// ViewModel for the AI Engine > Language Model > Cloud tab.
/// Queries all configured LLM provider APIs for available models,
/// allows enabling/disabling individual models, and displays token usage.
/// </summary>
public sealed partial class CloudLlmSettingsViewModel : ObservableObject
{
    private readonly ModelListService _modelListService;
    private readonly SettingsManager _settings;
    private readonly HistoryManager _history;
    private readonly DispatcherQueue _dispatcher;

    // ── Model catalog ────────────────────────────────────────────────────

    public ObservableCollection<ProviderModelGroup> ProviderGroups { get; } = [];

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private int _totalModelCount;

    [ObservableProperty]
    private int _enabledModelCount;

    // ── Usage summary ────────────────────────────────────────────────────

    [ObservableProperty]
    private string _usageTodayText = "";

    [ObservableProperty]
    private string _usageBreakdownText = "";

    [ObservableProperty]
    private bool _hasUsageData;

    // ── Default cloud provider + model picker (BUG-027) ──────────────────
    //
    // Two dropdowns. Provider is a fixed list (we know the set of providers
    // we support). Model is live-fetched from the selected provider's enabled
    // models — no hardcoded model IDs on this path.

    /// <summary>Display names for the default-provider ComboBox.</summary>
    public IReadOnlyList<string> AvailableProviderDisplayNames { get; } =
        ["Gemini", "OpenAI", "Anthropic", "OpenRouter", "Requesty"];

    /// <summary>Backing provider type identifiers parallel to <see cref="AvailableProviderDisplayNames"/>.</summary>
    private static readonly string[] AvailableProviderTypes =
        ["gemini", "openai", "anthropic", "openrouter", "requesty"];

    /// <summary>
    /// Naming-convention bridge between our internal provider type codes
    /// (used everywhere in settings/factory/router) and the human-readable
    /// Provider labels ModelListService attaches to each ModelInfo entry.
    /// This is NOT a hardcoded model list — just a translation table between
    /// two existing string conventions. Google/Gemini naming is historical.
    /// </summary>
    private static readonly Dictionary<string, string[]> ProviderTypeToListNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gemini"] = ["Google", "Google (Gemini)", "Gemini"],
            ["openai"] = ["OpenAI"],
            ["anthropic"] = ["Anthropic"],
            ["openrouter"] = ["OpenRouter"],
            ["requesty"] = ["Requesty"],
        };

    [ObservableProperty]
    private int _defaultProviderIndex = -1;

    /// <summary>
    /// Model choices populated from the live model list for the currently-
    /// selected provider, filtered to models the user has enabled.
    /// Empty list when: no provider picked, or no models enabled for this
    /// provider, or the model list hasn't loaded yet.
    /// </summary>
    public ObservableCollection<string> DefaultModelDisplayNames { get; } = [];

    /// <summary>Parallel list of real model IDs for <see cref="DefaultModelDisplayNames"/>.</summary>
    private readonly List<string> _defaultModelIds = [];

    [ObservableProperty]
    private int _defaultModelIndex = -1;

    /// <summary>True when the model dropdown is disabled (no models enabled for this provider).</summary>
    [ObservableProperty]
    private bool _defaultModelDropdownDisabled;

    /// <summary>
    /// Hint text shown when the model dropdown is disabled — typically
    /// "Enable at least one model for {Provider} first." Empty otherwise.
    /// </summary>
    [ObservableProperty]
    private string _defaultModelHint = "";

    /// <summary>
    /// Raised after the user picks a specific default model and persistence
    /// completes. The host page shows a ContentDialog asking whether to
    /// propagate the pick to all existing per-mode CloudProfile.ModelName
    /// entries. On Yes it calls <see cref="PropagateDefaultModelToAllModesAsync"/>.
    /// </summary>
    public event EventHandler<DefaultProviderChangeRequestedEventArgs>? DefaultProviderChangeRequested;

    // Guards so provider/model change handlers don't fire during programmatic
    // (non-user) updates: initial load from settings, provider switch that
    // repopulates the model list, etc.
    private bool _defaultProviderInitialized;
    private bool _suppressModelChanged;

    // ── Constructor ──────────────────────────────────────────────────────

    public CloudLlmSettingsViewModel(
        ModelListService modelListService,
        SettingsManager settings,
        HistoryManager history)
    {
        _modelListService = modelListService;
        _settings = settings;
        _history = history;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Load model catalog first, then initialize picker state. Picker
        // depends on ProviderGroups being populated to filter enabled models.
        _ = InitAsync();
    }

    private async Task InitAsync()
    {
        await LoadAsync().ConfigureAwait(false);

        _dispatcher.TryEnqueue(() =>
        {
            // Initial provider selection from settings. Triggers model-dropdown
            // refresh via OnDefaultProviderIndexChanged, but with
            // _defaultProviderInitialized=false we skip persistence + event.
            string currentProviderType = _settings.Current.CloudLlm.DefaultCloudLlmProvider;
            int pIdx = Array.FindIndex(AvailableProviderTypes,
                t => string.Equals(t, currentProviderType, StringComparison.OrdinalIgnoreCase));
            DefaultProviderIndex = pIdx >= 0 ? pIdx : 0;

            // Populate model dropdown for the loaded provider and select the
            // saved model if present. Also marks the picker as initialized so
            // subsequent user changes fire the normal handlers.
            RefreshDefaultModelDropdown(selectSavedModel: true);
            _defaultProviderInitialized = true;
        });
    }

    // ── Default picker — provider change ─────────────────────────────────

    partial void OnDefaultProviderIndexChanged(int value)
    {
        if (value < 0 || value >= AvailableProviderTypes.Length)
        {
            return;
        }

        // Repopulate the model dropdown for the new provider. During init
        // (_defaultProviderInitialized=false) we keep the saved model id so
        // RefreshDefaultModelDropdown selects it if still present. After init,
        // switching providers starts with no model selected until the user
        // confirms one.
        RefreshDefaultModelDropdown(selectSavedModel: !_defaultProviderInitialized);
    }

    // ── Default picker — model change (the real action) ──────────────────

    partial void OnDefaultModelIndexChanged(int value)
    {
        if (_suppressModelChanged || !_defaultProviderInitialized)
        {
            return;
        }
        if (value < 0 || value >= _defaultModelIds.Count)
        {
            return;
        }
        if (DefaultProviderIndex < 0 || DefaultProviderIndex >= AvailableProviderTypes.Length)
        {
            return;
        }

        string providerType = AvailableProviderTypes[DefaultProviderIndex];
        string providerDisplay = AvailableProviderDisplayNames[DefaultProviderIndex];
        string modelId = _defaultModelIds[value];
        string modelDisplay = DefaultModelDisplayNames[value];

        // Persist provider + model. Propagation is the optional second step,
        // offered via the event handler on the host page.
        _ = PersistDefaultProviderAndModelAsync(providerType, modelId);

        DefaultProviderChangeRequested?.Invoke(this, new DefaultProviderChangeRequestedEventArgs(
            providerType, providerDisplay, modelId, modelDisplay));
    }

    /// <summary>
    /// Rebuilds <see cref="DefaultModelDisplayNames"/> + <see cref="_defaultModelIds"/>
    /// from the currently-selected provider's enabled models in <see cref="ProviderGroups"/>.
    /// If <paramref name="selectSavedModel"/> is true, tries to select the model
    /// id saved in settings; otherwise selects nothing and leaves the user to pick.
    /// </summary>
    private void RefreshDefaultModelDropdown(bool selectSavedModel)
    {
        _suppressModelChanged = true;
        try
        {
            DefaultModelDisplayNames.Clear();
            _defaultModelIds.Clear();
            DefaultModelIndex = -1;

            if (DefaultProviderIndex < 0 || DefaultProviderIndex >= AvailableProviderTypes.Length)
            {
                DefaultModelDropdownDisabled = true;
                DefaultModelHint = "";
                return;
            }

            string providerType = AvailableProviderTypes[DefaultProviderIndex];
            string providerDisplay = AvailableProviderDisplayNames[DefaultProviderIndex];

            // Find the matching ProviderModelGroup — ModelListService labels
            // Gemini entries under "Google", etc. See ProviderTypeToListNames.
            if (!ProviderTypeToListNames.TryGetValue(providerType, out string[]? candidates))
            {
                candidates = [providerDisplay];
            }

            var group = ProviderGroups.FirstOrDefault(g =>
                candidates.Any(c => string.Equals(g.ProviderName, c, StringComparison.OrdinalIgnoreCase)));

            if (group is null)
            {
                // Model list hasn't loaded yet, or no models returned for this
                // provider (bad/missing API key). Leave the dropdown empty +
                // disabled with a hint so the user isn't confused.
                DefaultModelDropdownDisabled = true;
                DefaultModelHint = $"No {providerDisplay} models available — check that an API key is saved and click Refresh.";
                return;
            }

            var enabledModels = group.Models.Where(m => m.IsEnabled).ToList();
            if (enabledModels.Count == 0)
            {
                DefaultModelDropdownDisabled = true;
                DefaultModelHint = $"Enable at least one {providerDisplay} model below to pick a default.";
                return;
            }

            foreach (var m in enabledModels)
            {
                DefaultModelDisplayNames.Add(m.DisplayName);
                _defaultModelIds.Add(m.ModelId);
            }

            DefaultModelDropdownDisabled = false;
            DefaultModelHint = "";

            if (selectSavedModel)
            {
                string savedModelId = _settings.Current.CloudLlm.DefaultCloudLlmModelId;
                int idx = _defaultModelIds.FindIndex(id =>
                    string.Equals(id, savedModelId, StringComparison.Ordinal));
                DefaultModelIndex = idx >= 0 ? idx : -1;
            }
        }
        finally
        {
            _suppressModelChanged = false;
        }
    }

    /// <summary>
    /// Public hook called after the model catalog reload (Refresh button) so
    /// the picker can re-sync its model dropdown against the latest list.
    /// </summary>
    public void OnProviderGroupsRefreshed()
    {
        _dispatcher.TryEnqueue(() => RefreshDefaultModelDropdown(selectSavedModel: true));
    }

    private async Task PersistDefaultProviderAndModelAsync(string providerType, string modelId)
    {
        try
        {
            var updated = _settings.Current with
            {
                CloudLlm = _settings.Current.CloudLlm with
                {
                    DefaultCloudLlmProvider = providerType,
                    DefaultCloudLlmModelId = modelId,
                },
            };
            await _settings.UpdateAsync(updated).ConfigureAwait(false);
            Log.Information("CloudLlm: default set to {Provider} / {Model}", providerType, modelId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CloudLlm: failed to persist default provider/model");
        }
    }

    /// <summary>
    /// Rewrites every DictationModes[].CloudProfile.ModelName and
    /// UtilityPipelines[].CloudProfile.ModelName to the user-picked
    /// <paramref name="modelId"/>. Model ID is written verbatim — no
    /// canonical substitution. Called by the host page when the user
    /// confirms the propagation dialog.
    /// </summary>
    public async Task PropagateDefaultModelToAllModesAsync(string modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            Log.Warning("CloudLlm: propagation skipped — empty model id");
            return;
        }

        try
        {
            var s = _settings.Current;
            var updated = s with
            {
                DictationModes = [.. s.DictationModes.Select(m => m with
                {
                    CloudProfile = m.CloudProfile with { ModelName = modelId },
                })],
                UtilityPipelines = [.. s.UtilityPipelines.Select(p => p with
                {
                    CloudProfile = p.CloudProfile with { ModelName = modelId },
                })],
            };
            await _settings.UpdateAsync(updated).ConfigureAwait(false);
            Log.Information(
                "CloudLlm: propagated default model {Model} to all DictationModes + UtilityPipelines",
                modelId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CloudLlm: failed to propagate default model");
        }
    }

    // ── Canonical default model per provider ─────────────────────────────
    // Matches WizardViewModel.DefaultModelForProvider + LLMProviderFactory.ResolveModel.
    // When the "all off except canonical" defaults are first applied, these are the
    // single models left enabled per provider. Keys match ModelListService's Provider
    // display names (see ModelListService.cs fetch methods).

    private static readonly Dictionary<string, string> CanonicalByProvider =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Anthropic"] = "claude-haiku-4-5-20251001",
            ["Google (Gemini)"] = "gemini-2.5-flash",
            ["Gemini"] = "gemini-2.5-flash",
            ["OpenAI"] = "gpt-4o-mini",
            ["OpenRouter"] = "openai/gpt-4o-mini",
            ["Requesty"] = "openai/gpt-4o-mini",
        };

    // ── Commands ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        await LoadModelsAsync().ConfigureAwait(false);

        // After the model catalog reloads, the default picker needs to
        // refresh its model dropdown — the old list may be stale.
        OnProviderGroupsRefreshed();
    }

    // ── Load all data ────────────────────────────────────────────────────

    private async Task LoadAsync()
    {
        await Task.WhenAll(
            LoadModelsAsync(),
            LoadUsageAsync()
        ).ConfigureAwait(false);
    }

    // ── Model catalog loading ────────────────────────────────────────────

    private async Task LoadModelsAsync()
    {
        _dispatcher.TryEnqueue(() =>
        {
            IsLoading = true;
            StatusText = "Querying providers...";
        });

        try
        {
            var allModels = await _modelListService.GetAvailableModelsAsync().ConfigureAwait(false);

            // Exclude local (Ollama) models — those are managed in the Local tab
            var cloudModels = allModels
                .Where(m => !string.Equals(m.Provider, "Ollama (Local)", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // First-run default filter: keep one canonical model per provider enabled,
            // disable everything else. Prevents fresh installs from drowning the user
            // in 400+ models in the dropdowns (BUG-028).
            if (!_settings.Current.CloudLlm.DefaultsApplied && cloudModels.Count > 0)
            {
                var disabledDefaults = ComputeDefaultDisabledSet(cloudModels);
                var updated = _settings.Current with
                {
                    CloudLlm = _settings.Current.CloudLlm with
                    {
                        DisabledModelIds = disabledDefaults,
                        DefaultsApplied = true,
                    },
                };
                await _settings.UpdateAsync(updated).ConfigureAwait(false);
                Log.Information("CloudLlmSettings: applied first-run defaults — disabled {N} of {T} models",
                    disabledDefaults.Count, cloudModels.Count);
            }

            var disabledIds = new HashSet<string>(_settings.Current.CloudLlm.DisabledModelIds, StringComparer.Ordinal);

            // Group by provider
            var groups = cloudModels
                .GroupBy(m => m.Provider, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g =>
                {
                    var items = g
                        .OrderBy(m => m.DisplayName, StringComparer.Ordinal)
                        .Select(m => new ModelToggleItem(
                            m.ModelId,
                            m.DisplayName,
                            m.Provider,
                            !disabledIds.Contains(m.ModelId),
                            OnModelToggled))
                        .ToList();

                    var group = new ProviderModelGroup
                    {
                        ProviderName = g.Key,
                        Models = new ObservableCollection<ModelToggleItem>(items),
                        OnAllToggled = OnProviderAllToggled,
                    };
                    group.RefreshIsAnyEnabled();
                    return group;
                })
                .ToList();

            int total = cloudModels.Count;
            int enabled = total - disabledIds.Count(id => cloudModels.Any(m => string.Equals(m.ModelId, id, StringComparison.Ordinal)));

            _dispatcher.TryEnqueue(() =>
            {
                ProviderGroups.Clear();
                foreach (var group in groups)
                {
                    ProviderGroups.Add(group);
                }

                TotalModelCount = total;
                EnabledModelCount = enabled;
                StatusText = total > 0
                    ? $"{enabled} of {total} models enabled"
                    : "No models found. Check your API keys.";
                IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CloudLlmSettingsViewModel: failed to load models");
            _dispatcher.TryEnqueue(() =>
            {
                StatusText = "Failed to query models. Check your API keys and try again.";
                IsLoading = false;
            });
        }
    }

    /// <summary>
    /// First-run default: disable everything except one canonical model per provider.
    /// Falls back to the alphabetically-first model if the canonical isn't in the list
    /// (provider may have deprecated/renamed it).
    /// </summary>
    private static List<string> ComputeDefaultDisabledSet(IReadOnlyList<ModelInfo> cloudModels)
    {
        var enabledPerProvider = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in cloudModels.GroupBy(m => m.Provider, StringComparer.Ordinal))
        {
            string? chosen = null;
            if (CanonicalByProvider.TryGetValue(group.Key, out var canonical))
            {
                chosen = group.FirstOrDefault(m =>
                    string.Equals(m.ModelId, canonical, StringComparison.Ordinal))?.ModelId;
            }

            chosen ??= group.OrderBy(m => m.DisplayName, StringComparer.Ordinal).First().ModelId;
            enabledPerProvider.Add(chosen);
        }

        return cloudModels
            .Where(m => !enabledPerProvider.Contains(m.ModelId))
            .Select(m => m.ModelId)
            .ToList();
    }

    // ── Usage loading ────────────────────────────────────────────────────

    private async Task LoadUsageAsync()
    {
        try
        {
            var today = DateTimeOffset.UtcNow.Date;
            var usage = await _history.GetUsageSummaryAsync(today).ConfigureAwait(false);

            long totalTokens = usage.Sum(u => u.TotalTokens);
            int totalRequests = usage.Sum(u => u.RequestCount);

            string breakdown = string.Join(" | ",
                usage.Select(u => $"{u.Provider}: {u.TotalTokens:N0}"));

            _dispatcher.TryEnqueue(() =>
            {
                HasUsageData = totalTokens > 0;
                UsageTodayText = totalTokens > 0
                    ? $"{totalTokens:N0} tokens ({totalRequests:N0} requests)"
                    : "No usage recorded today";
                UsageBreakdownText = breakdown;
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "CloudLlmSettingsViewModel: failed to load usage");
        }
    }

    // ── Individual model toggle handler ──────────────────────────────────

    private void OnModelToggled(ModelToggleItem item)
    {
        var current = _settings.Current.CloudLlm.DisabledModelIds;
        var updated = new List<string>(current);

        if (item.IsEnabled)
        {
            updated.Remove(item.ModelId);
        }
        else
        {
            if (!updated.Contains(item.ModelId, StringComparer.Ordinal))
            {
                updated.Add(item.ModelId);
            }
        }

        PersistDisabledIds(updated);

        // Sync the provider group's master toggle display without cascading
        foreach (var group in ProviderGroups)
        {
            if (string.Equals(group.ProviderName, item.Provider, StringComparison.Ordinal))
            {
                group.RefreshIsAnyEnabled();
                break;
            }
        }
    }

    // ── Provider-level master toggle handler ─────────────────────────────

    private void OnProviderAllToggled(ProviderModelGroup group, bool newValue)
    {
        // Cascade to all children, then persist in a single save.
        var current = _settings.Current.CloudLlm.DisabledModelIds;
        var updated = new HashSet<string>(current, StringComparer.Ordinal);

        foreach (var model in group.Models)
        {
            model.SetIsEnabledSilent(newValue);
            if (newValue)
            {
                updated.Remove(model.ModelId);
            }
            else
            {
                updated.Add(model.ModelId);
            }
        }

        PersistDisabledIds([.. updated]);
    }

    private void PersistDisabledIds(List<string> disabledIds)
    {
        var newSettings = _settings.Current with
        {
            CloudLlm = _settings.Current.CloudLlm with
            {
                DisabledModelIds = disabledIds,
                DefaultsApplied = true, // any user interaction counts as curation
            },
        };

        int enabled = TotalModelCount - disabledIds.Count;
        EnabledModelCount = enabled > 0 ? enabled : 0;
        StatusText = $"{EnabledModelCount} of {TotalModelCount} models enabled";

        _ = _settings.UpdateAsync(newSettings).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "CloudLlmSettingsViewModel: failed to save disabled models");
            }
        }, TaskScheduler.Default);
    }
}

/// <summary>
/// A group of models from a single LLM provider (e.g., "OpenAI", "Anthropic").
/// Has a master toggle that bulk-enables or bulk-disables all models in the group.
/// </summary>
public sealed partial class ProviderModelGroup : ObservableObject
{
    private bool _muteMasterChanged;

    public required string ProviderName { get; init; }
    public required ObservableCollection<ModelToggleItem> Models { get; init; } = [];

    /// <summary>
    /// Invoked when the master toggle is flipped by the user (not when synced from children).
    /// Owner is expected to cascade the new value to all <see cref="Models"/> and persist.
    /// </summary>
    public required Action<ProviderModelGroup, bool> OnAllToggled { get; init; }

    public int ModelCount => Models.Count;

    /// <summary>
    /// Master toggle. True = at least one model in this group is enabled.
    /// Flipping OFF disables all; flipping ON enables all.
    /// </summary>
    [ObservableProperty]
    private bool _isAnyEnabled;

    /// <summary>
    /// Recomputes <see cref="IsAnyEnabled"/> from the current child state without
    /// triggering the user-initiated cascade.
    /// </summary>
    public void RefreshIsAnyEnabled()
    {
        _muteMasterChanged = true;
        IsAnyEnabled = Models.Any(m => m.IsEnabled);
        _muteMasterChanged = false;
    }

    partial void OnIsAnyEnabledChanged(bool value)
    {
        if (_muteMasterChanged)
        {
            return;
        }

        OnAllToggled(this, value);
    }
}

/// <summary>
/// A single model entry with an enable/disable toggle.
/// </summary>
public sealed partial class ModelToggleItem : ObservableObject
{
    private readonly Action<ModelToggleItem> _onToggled;
    private bool _muteToggle;

    public string ModelId { get; }
    public string DisplayName { get; }
    public string Provider { get; }

    [ObservableProperty]
    private bool _isEnabled;

    public ModelToggleItem(string modelId, string displayName, string provider, bool isEnabled, Action<ModelToggleItem> onToggled)
    {
        ModelId = modelId;
        DisplayName = displayName;
        Provider = provider;
        _isEnabled = isEnabled;
        _onToggled = onToggled;
    }

    /// <summary>
    /// Sets <see cref="IsEnabled"/> without firing the toggle callback.
    /// Used when the provider-level master toggle cascades state to children —
    /// the owner persists once in a single batched save instead of N times.
    /// </summary>
    public void SetIsEnabledSilent(bool value)
    {
        if (IsEnabled == value)
        {
            return;
        }

        _muteToggle = true;
        IsEnabled = value;
        _muteToggle = false;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_muteToggle)
        {
            return;
        }

        _onToggled(this);
    }
}

/// <summary>
/// Payload for <see cref="CloudLlmSettingsViewModel.DefaultProviderChangeRequested"/>.
/// Carries the provider + model info so the host page can render a
/// ContentDialog and, on user confirmation, call
/// <see cref="CloudLlmSettingsViewModel.PropagateDefaultModelToAllModesAsync"/>
/// with the user-picked model ID — never a hardcoded canonical.
/// </summary>
public sealed class DefaultProviderChangeRequestedEventArgs : EventArgs
{
    public string ProviderType { get; }
    public string ProviderDisplayName { get; }
    public string ModelId { get; }
    public string ModelDisplayName { get; }

    public DefaultProviderChangeRequestedEventArgs(
        string providerType,
        string providerDisplayName,
        string modelId,
        string modelDisplayName)
    {
        ProviderType = providerType;
        ProviderDisplayName = providerDisplayName;
        ModelId = modelId;
        ModelDisplayName = modelDisplayName;
    }
}
