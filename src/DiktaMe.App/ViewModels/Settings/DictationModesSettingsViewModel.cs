
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// ViewModel for the Dictation Modes settings page.
/// Provides CRUD for dictation modes and update-only for utility pipelines.
/// Uses ModelListService for real API model discovery.
/// </summary>
public sealed partial class DictationModesSettingsViewModel : ObservableObject
{
    private readonly DictationModeManager _modeManager;
    private readonly SettingsManager _settings;
    private readonly ModelListService _modelListService;

    // ── Left sidebar list ──────────────────────────────────────────────────

    /// <summary>Items displayed in the left sidebar (dictation modes + utility pipelines).</summary>
    public ObservableCollection<ModeListItem> ModeItems { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    // ── Right panel fields ─────────────────────────────────────────────────

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private bool _isBuiltIn;

    [ObservableProperty]
    private bool _isDictationMode;

    // ── Cloud profile fields ───────────────────────────────────────────────

    [ObservableProperty]
    private string _cloudSystemPrompt = "";

    [ObservableProperty]
    private bool _cloudUseLlm = true;

    [ObservableProperty]
    private int _selectedCloudModelIndex;

    // ── Local profile fields ───────────────────────────────────────────────

    [ObservableProperty]
    private string _localSystemPrompt = "";

    [ObservableProperty]
    private bool _localUseLlm = true;

    // ── Model list ─────────────────────────────────────────────────────────

    /// <summary>Display names for the Cloud model ComboBox.</summary>
    public ObservableCollection<string> CloudModelNames { get; } = [];

    /// <summary>Backing model IDs (parallel to CloudModelNames).</summary>
    private readonly List<string> _cloudModelIds = [];

    [ObservableProperty]
    private bool _isLoadingModels;

    [ObservableProperty]
    private bool _hasSelection;

    // ── Constructor ────────────────────────────────────────────────────────

    public DictationModesSettingsViewModel(
        DictationModeManager modeManager,
        SettingsManager settings,
        ModelListService modelListService)
    {
        _modeManager = modeManager;
        _settings = settings;
        _modelListService = modelListService;

        LoadModeList();
        _ = LoadModelsAsync();
    }

    // ── List loading ───────────────────────────────────────────────────────

    private void LoadModeList()
    {
        ModeItems.Clear();

        // Add dictation modes (CRUD) - only dictation, not utility pipelines
        foreach (var mode in _modeManager.GetAllModes())
        {
            ModeItems.Add(new ModeListItem
            {
                Id = mode.Id,
                Title = mode.Title,
                IsBuiltIn = mode.IsBuiltIn,
                IsDictationMode = true,
                Subtitle = mode.IsBuiltIn ? "Built-in" : "Custom",
            });
        }

        // Select first item if available
        if (ModeItems.Count > 0)
        {
            SelectedIndex = 0;
        }
    }

    // ── Model list loading (real API) ──────────────────────────────────────

    private async Task LoadModelsAsync()
    {
        IsLoadingModels = true;

        try
        {
            var models = await _modelListService.GetAvailableModelsAsync().ConfigureAwait(false);

            // Always dispatch to UI thread (remove dual-branch logic)
            if (App.Current.MainWindow?.DispatcherQueue is { } dispatcher)
            {
                dispatcher.TryEnqueue(() =>
                {
                    PopulateModelList(models);
                    IsLoadingModels = false;  // Set on UI thread
                });
            }
            else
            {
                // Fallback for unit tests
                PopulateModelList(models);
                IsLoadingModels = false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DictationModesSettingsViewModel: Failed to load models");
            IsLoadingModels = false;
        }
    }

    private void PopulateModelList(List<ModelInfo> models)
    {
        CloudModelNames.Clear();
        _cloudModelIds.Clear();

        // First entry: "Default (provider default)"
        CloudModelNames.Add("(Default — use provider default)");
        _cloudModelIds.Add("");

        // Filter out Ollama models - Cloud profile should only show cloud API providers
        var cloudModels = models.Where(m => !string.Equals(m.Provider, "Ollama (Local)", StringComparison.OrdinalIgnoreCase))
                                .OrderBy(m => m.Provider, StringComparer.OrdinalIgnoreCase)
                                .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase);

        foreach (var m in cloudModels)
        {
            CloudModelNames.Add($"{m.DisplayName}  ({m.Provider})");
            _cloudModelIds.Add(m.ModelId);
        }

        // Re-select current model after repopulation
        if (HasSelection)
        {
            LoadDetail();
        }
    }

    // ── Selection change ───────────────────────────────────────────────────

    partial void OnSelectedIndexChanged(int value)
    {
        if (value >= 0 && value < ModeItems.Count && !ModeItems[value].IsSeparator)
        {
            HasSelection = true;
            LoadDetail();
        }
        else
        {
            HasSelection = false;
        }
    }

    private void LoadDetail()
    {
        if (SelectedIndex < 0 || SelectedIndex >= ModeItems.Count)
        {
            return;
        }

        var item = ModeItems[SelectedIndex];
        if (item.IsSeparator)
        {
            return;
        }

        Title = item.Title;
        IsBuiltIn = item.IsBuiltIn;
        IsDictationMode = item.IsDictationMode;

        // Only load dictation modes - no utility pipelines
        LoadDictationModeDetail(item.Id);
    }

    private void LoadDictationModeDetail(string modeId)
    {
        var mode = _modeManager.GetModeById(modeId);
        if (mode is null)
        {
            return;
        }

        // Cloud profile
        CloudSystemPrompt = mode.CloudProfile.SystemPrompt ?? "";
        CloudUseLlm = mode.CloudProfile.UseLlm;
        SelectedCloudModelIndex = FindModelIndex(mode.CloudProfile.ModelName);

        // Local profile
        LocalSystemPrompt = mode.LocalProfile.SystemPrompt ?? "";
        LocalUseLlm = mode.LocalProfile.UseLlm;
    }


    private int FindModelIndex(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return 0; // "(Default)" entry
        }

        int idx = _cloudModelIds.IndexOf(modelName);
        return idx >= 0 ? idx : 0;
    }

    // ── Save ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedIndex < 0 || SelectedIndex >= ModeItems.Count)
        {
            return;
        }

        var item = ModeItems[SelectedIndex];
        if (item.IsSeparator)
        {
            return;
        }

        try
        {
            // Only save dictation modes
            await SaveDictationModeAsync(item.Id).ConfigureAwait(true);

            // Update sidebar title
            item.Title = Title;
            int idx = SelectedIndex;
            LoadModeList();
            SelectedIndex = idx;

            Log.Information("DictationModesSettingsVM: Saved {Id}", item.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DictationModesSettingsVM: Failed to save {Id}", item.Id);
        }
    }

    private async Task SaveDictationModeAsync(string modeId)
    {
        string? cloudModel = SelectedCloudModelIndex > 0 && SelectedCloudModelIndex < _cloudModelIds.Count
            ? _cloudModelIds[SelectedCloudModelIndex]
            : null;

        var cloudProfile = new DictationProfile
        {
            SystemPrompt = string.IsNullOrWhiteSpace(CloudSystemPrompt) ? null : CloudSystemPrompt,
            UseLlm = CloudUseLlm,
            ModelName = cloudModel,
            Hotkey = null,
        };

        var localProfile = new DictationProfile
        {
            SystemPrompt = string.IsNullOrWhiteSpace(LocalSystemPrompt) ? null : LocalSystemPrompt,
            UseLlm = LocalUseLlm,
            ModelName = null, // Local always uses global Ollama model
            Hotkey = null,
        };

        await _modeManager.UpdateModeAsync(modeId, Title, cloudProfile, localProfile).ConfigureAwait(false);
    }


    // ── Create Mode ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreateModeAsync()
    {
        try
        {
            var cloudProfile = new DictationProfile
            {
                SystemPrompt = PromptDefaults.Dictate,
                UseLlm = true,
                ModelName = null,
                Hotkey = null,
            };

            var localProfile = new DictationProfile
            {
                SystemPrompt = PromptDefaults.Dictate,
                UseLlm = true,
                ModelName = null,
                Hotkey = null,
            };

            var newMode = await _modeManager.CreateModeAsync("New Mode", cloudProfile, localProfile)
                .ConfigureAwait(true);

            // Refresh list and select the new mode
            LoadModeList();

            // Find the new mode in the list
            for (int i = 0; i < ModeItems.Count; i++)
            {
                if (string.Equals(ModeItems[i].Id, newMode.Id, StringComparison.Ordinal))
                {
                    SelectedIndex = i;
                    break;
                }
            }

            Log.Information("DictationModesSettingsVM: Created new mode '{Title}'", newMode.Title);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DictationModesSettingsVM: Failed to create mode");
        }
    }

    // ── Delete Mode ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteModeAsync()
    {
        if (SelectedIndex < 0 || SelectedIndex >= ModeItems.Count)
        {
            return;
        }

        var item = ModeItems[SelectedIndex];
        if (item.IsSeparator || item.IsBuiltIn || !item.IsDictationMode)
        {
            return; // Cannot delete built-ins, separators, or utility pipelines
        }

        try
        {
            await _modeManager.DeleteModeAsync(item.Id).ConfigureAwait(true);

            LoadModeList();

            // Select previous item or first
            SelectedIndex = Math.Min(SelectedIndex, ModeItems.Count - 1);

            Log.Information("DictationModesSettingsVM: Deleted mode '{Title}'", item.Title);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DictationModesSettingsVM: Failed to delete mode {Id}", item.Id);
        }
    }

    // ── Move Up / Move Down ────────────────────────────────────────────────

    [RelayCommand]
    private async Task MoveUpAsync()
    {
        if (SelectedIndex <= 0 || SelectedIndex >= ModeItems.Count)
        {
            return;
        }

        var item = ModeItems[SelectedIndex];
        if (!item.IsDictationMode)
        {
            return; // Only dictation modes can be reordered
        }

        // Find dictation mode items only
        var modeIds = ModeItems.Where(m => m.IsDictationMode).Select(m => m.Id).ToList();
        int modeIdx = modeIds.IndexOf(item.Id);
        if (modeIdx <= 0)
        {
            return;
        }

        // Swap with previous
        (modeIds[modeIdx], modeIds[modeIdx - 1]) = (modeIds[modeIdx - 1], modeIds[modeIdx]);

        try
        {
            await _modeManager.ReorderModesAsync(modeIds).ConfigureAwait(true);
            int newIndex = SelectedIndex - 1;
            LoadModeList();
            SelectedIndex = newIndex;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DictationModesSettingsVM: Failed to move mode up");
        }
    }

    [RelayCommand]
    private async Task MoveDownAsync()
    {
        if (SelectedIndex < 0 || SelectedIndex >= ModeItems.Count)
        {
            return;
        }

        var item = ModeItems[SelectedIndex];
        if (!item.IsDictationMode)
        {
            return;
        }

        var modeIds = ModeItems.Where(m => m.IsDictationMode).Select(m => m.Id).ToList();
        int modeIdx = modeIds.IndexOf(item.Id);
        if (modeIdx < 0 || modeIdx >= modeIds.Count - 1)
        {
            return;
        }

        // Swap with next
        (modeIds[modeIdx], modeIds[modeIdx + 1]) = (modeIds[modeIdx + 1], modeIds[modeIdx]);

        try
        {
            await _modeManager.ReorderModesAsync(modeIds).ConfigureAwait(true);
            int newIndex = SelectedIndex + 1;
            LoadModeList();
            SelectedIndex = newIndex;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DictationModesSettingsVM: Failed to move mode down");
        }
    }

    // ── Refresh models ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        await LoadModelsAsync().ConfigureAwait(true);
    }
}

// ── List item model ──────────────────────────────────────────────────────

/// <summary>
/// Represents an item in the left sidebar list (dictation mode, utility pipeline, or separator).
/// </summary>
public sealed class ModeListItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; set; }
    public bool IsBuiltIn { get; init; }
    public bool IsDictationMode { get; init; }
    public bool IsSeparator { get; init; }
    public string Subtitle { get; init; } = "";
}
