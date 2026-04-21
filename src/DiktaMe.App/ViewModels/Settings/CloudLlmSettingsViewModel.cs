
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

        _ = LoadAsync();
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
