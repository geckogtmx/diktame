
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
            var disabledIds = new HashSet<string>(_settings.Current.CloudLlm.DisabledModelIds, StringComparer.Ordinal);

            // Exclude local (Ollama) models — those are managed in the Local tab
            var cloudModels = allModels
                .Where(m => !string.Equals(m.Provider, "Ollama (Local)", StringComparison.OrdinalIgnoreCase))
                .ToList();

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

                    return new ProviderModelGroup
                    {
                        ProviderName = g.Key,
                        Models = new ObservableCollection<ModelToggleItem>(items),
                    };
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

    // ── Toggle handler ───────────────────────────────────────────────────

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

        var newSettings = _settings.Current with
        {
            CloudLlm = _settings.Current.CloudLlm with
            {
                DisabledModelIds = updated,
            },
        };

        int enabled = TotalModelCount - updated.Count;
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
/// </summary>
public sealed class ProviderModelGroup
{
    public required string ProviderName { get; init; }
    public ObservableCollection<ModelToggleItem> Models { get; init; } = [];
    public int ModelCount => Models.Count;
}

/// <summary>
/// A single model entry with an enable/disable toggle.
/// </summary>
public sealed partial class ModelToggleItem : ObservableObject
{
    private readonly Action<ModelToggleItem> _onToggled;

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

    partial void OnIsEnabledChanged(bool value)
    {
        _onToggled(this);
    }
}
