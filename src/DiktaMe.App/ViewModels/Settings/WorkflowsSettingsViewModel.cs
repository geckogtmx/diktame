
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// Host ViewModel for the Workflows &amp; Modes settings page.
/// Aggregates ModesSettingsViewModel (utility pipelines).
/// Owns the "Dictation Behaviors" fields (AdditionalKey, TrailingSpace, RawModeOverride, RefineVoiceMode).
/// </summary>
public sealed partial class WorkflowsSettingsViewModel : ObservableObject
{
    public ModesSettingsViewModel UtilityPipelines { get; }

    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;
    private bool _isLoading;

    // ── Inner list ───────────────────────────────────────────────────────

    public ObservableCollection<ModeListItem> SubItems { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isDictationBehaviorsSelected;

    [ObservableProperty]
    private bool _isUtilityPipelineSelected;

    // ── Dictation Behaviors fields ───────────────────────────────────────

    [ObservableProperty]
    private int _selectedAdditionalKeyIndex;

    [ObservableProperty]
    private bool _trailingSpace = true;

    [ObservableProperty]
    private bool _rawModeOverride;

    [ObservableProperty]
    private bool _refineVoiceMode;

    public string[] AdditionalKeyOptions => [
        _loc.GetString("Settings_General_AdditionalKey_None"),
        _loc.GetString("Settings_General_AdditionalKey_Enter"),
        _loc.GetString("Settings_General_AdditionalKey_Tab"),
        _loc.GetString("Settings_General_AdditionalKey_Space"),
    ];
    public string[] AdditionalKeyCodes { get; } = ["", "Enter", "Tab", "Space"];

    // ── Constructor ─────────────────────────────────────────────────────

    public WorkflowsSettingsViewModel(
        ModesSettingsViewModel utilityPipelines,
        SettingsManager settings,
        LocalizationService loc)
    {
        UtilityPipelines = utilityPipelines;
        _settings = settings;
        _loc = loc;

        LoadSubItems();
        LoadDictationBehaviors();

        if (SubItems.Count > 0)
        {
            SelectedIndex = 0;
        }
    }

    // ── Sub-item list ───────────────────────────────────────────────────

    private void LoadSubItems()
    {
        SubItems.Clear();
        SubItems.Add(new ModeListItem { Id = "behaviors", Title = _loc.GetString("Settings_Workflows_Behaviors"), IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "ask", Title = "Ask", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "refine_auto", Title = "Refine (Auto)", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "refine_instruction", Title = "Refine (Verbal)", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "translate", Title = "Translate", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "note", Title = "Notes", IsDictationMode = false, IsSeparator = false });
    }

    // ── Load / Save dictation behaviors ─────────────────────────────────

    private void LoadDictationBehaviors()
    {
        _isLoading = true;
        var g = _settings.Current.General;
        SelectedAdditionalKeyIndex = Array.IndexOf(AdditionalKeyCodes, g.AdditionalKey) is var i and >= 0 ? i : 0;
        TrailingSpace = g.TrailingSpace;
        RawModeOverride = g.RawModeOverride;
        RefineVoiceMode = g.RefineVoiceMode;
        _isLoading = false;
    }

    partial void OnSelectedAdditionalKeyIndexChanged(int value) => SaveDictationBehaviors();
    partial void OnTrailingSpaceChanged(bool value) => SaveDictationBehaviors();
    partial void OnRawModeOverrideChanged(bool value) => SaveDictationBehaviors();
    partial void OnRefineVoiceModeChanged(bool value) => SaveDictationBehaviors();

    private void SaveDictationBehaviors()
    {
        if (_isLoading)
        {
            return;
        }

        var updated = _settings.Current with
        {
            General = _settings.Current.General with
            {
                AdditionalKey = SelectedAdditionalKeyIndex >= 0 && SelectedAdditionalKeyIndex < AdditionalKeyCodes.Length
                    ? AdditionalKeyCodes[SelectedAdditionalKeyIndex] : "",
                TrailingSpace = TrailingSpace,
                RawModeOverride = RawModeOverride,
                RefineVoiceMode = RefineVoiceMode,
            }
        };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save dictation behaviors");
            }
        }, TaskScheduler.Default);
    }

    // ── Selection change ────────────────────────────────────────────────

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < SubItems.Count;

        if (!HasSelection)
        {
            IsDictationBehaviorsSelected = false;
            IsUtilityPipelineSelected = false;
            return;
        }

        string id = SubItems[value].Id;
        IsDictationBehaviorsSelected = id == "behaviors";
        IsUtilityPipelineSelected = !IsDictationBehaviorsSelected;

        // Sync the utility pipelines inner list selection when a pipeline sub-item is clicked
        if (IsUtilityPipelineSelected)
        {
            for (int i = 0; i < UtilityPipelines.ModeItems.Count; i++)
            {
                if (string.Equals(UtilityPipelines.ModeItems[i].Id, id, StringComparison.Ordinal))
                {
                    UtilityPipelines.SelectedIndex = i;
                    break;
                }
            }
        }
    }
}
