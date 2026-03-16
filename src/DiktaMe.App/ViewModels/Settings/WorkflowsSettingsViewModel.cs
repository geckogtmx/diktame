
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// Host ViewModel for the Pipelines settings page.
/// Aggregates ModesSettingsViewModel (utility pipelines) and TtsSettingsViewModel (speak toggles).
/// </summary>
public sealed partial class WorkflowsSettingsViewModel : ObservableObject
{
    public ModesSettingsViewModel UtilityPipelines { get; }
    public TtsSettingsViewModel Tts { get; }

    private readonly LocalizationService _loc;

    // ── Inner list ───────────────────────────────────────────────────────

    public ObservableCollection<ModeListItem> SubItems { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isSpeakSelected;

    [ObservableProperty]
    private bool _isUtilityPipelineSelected;

    // ── Constructor ─────────────────────────────────────────────────────

    public WorkflowsSettingsViewModel(
        ModesSettingsViewModel utilityPipelines,
        TtsSettingsViewModel tts,
        SettingsManager settings,
        LocalizationService loc)
    {
        UtilityPipelines = utilityPipelines;
        Tts = tts;
        _loc = loc;

        LoadSubItems();

        if (SubItems.Count > 0)
        {
            SelectedIndex = 0;
        }
    }

    // ── Sub-item list ───────────────────────────────────────────────────

    private void LoadSubItems()
    {
        SubItems.Clear();
        SubItems.Add(new ModeListItem { Id = "ask", Title = "Ask", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "refine_auto", Title = "Refine (Auto)", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "refine_instruction", Title = "Refine (Verbal)", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "translate", Title = "Translate", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "note", Title = "Notes", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "speak", Title = _loc.GetString("Settings_Workflows_Speak"), IsDictationMode = false, IsSeparator = false });
    }

    // ── Selection change ────────────────────────────────────────────────

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < SubItems.Count;

        if (!HasSelection)
        {
            IsSpeakSelected = false;
            IsUtilityPipelineSelected = false;
            return;
        }

        string id = SubItems[value].Id;
        IsSpeakSelected = id == "speak";
        IsUtilityPipelineSelected = !IsSpeakSelected;

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
