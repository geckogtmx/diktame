
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// Host ViewModel for the Pipelines settings page.
/// Aggregates ModesSettingsViewModel (utility pipelines), TtsSettingsViewModel (speak toggles),
/// and Vision pipeline settings.
/// </summary>
public sealed partial class WorkflowsSettingsViewModel : ObservableObject
{
    public ModesSettingsViewModel UtilityPipelines { get; }
    public TtsSettingsViewModel Tts { get; }

    private readonly SettingsManager _settings;
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
    private bool _isVisionSelected;

    [ObservableProperty]
    private bool _isUtilityPipelineSelected;

    // ── Vision pipeline settings ─────────────────────────────────────────

    [ObservableProperty]
    private bool _visionEnabled = true;

    [ObservableProperty]
    private string _visionDefaultQuery = "";

    [ObservableProperty]
    private int _visionOutputModeIndex;

    [ObservableProperty]
    private bool _visionAutoRecordQuery = true;

    [ObservableProperty]
    private int _visionMaxResponseTokens = 4096;

    [ObservableProperty]
    private int _visionMaxImageDimensionPx = 2048;

    // Cloud/Local tab for Vision pipeline
    [ObservableProperty]
    private bool _isVisionCloudTab = true;

    [ObservableProperty]
    private string _visionCloudSystemPrompt = "";

    [ObservableProperty]
    private string _visionLocalSystemPrompt = "";

    public string[] VisionOutputModes => ["Inject at cursor", "Clipboard only", "Toast only", "Toast + Inject", "Toast + Clipboard"];
    private static readonly string[] OutputModeCodes = ["inject", "clipboard", "toast", "toast_inject", "toast_clipboard"];

    // ── Constructor ─────────────────────────────────────────────────────

    public WorkflowsSettingsViewModel(
        ModesSettingsViewModel utilityPipelines,
        TtsSettingsViewModel tts,
        SettingsManager settings,
        LocalizationService loc)
    {
        UtilityPipelines = utilityPipelines;
        Tts = tts;
        _settings = settings;
        _loc = loc;

        LoadSubItems();
        LoadVisionSettings();

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
        SubItems.Add(new ModeListItem { Id = "vision", Title = "Vision", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "speak", Title = _loc.GetString("Settings_Workflows_Speak"), IsDictationMode = false, IsSeparator = false });
    }

    // ── Selection change ────────────────────────────────────────────────

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < SubItems.Count;

        if (!HasSelection)
        {
            IsSpeakSelected = false;
            IsVisionSelected = false;
            IsUtilityPipelineSelected = false;
            return;
        }

        string id = SubItems[value].Id;
        IsSpeakSelected = id == "speak";
        IsVisionSelected = id == "vision";
        IsUtilityPipelineSelected = !IsSpeakSelected && !IsVisionSelected;

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

    // ── Vision Cloud/Local commands ─────────────────────────────────────

    [RelayCommand] private void SelectVisionCloud() => IsVisionCloudTab = true;
    [RelayCommand] private void SelectVisionLocal() => IsVisionCloudTab = false;

    // ── Vision settings ─────────────────────────────────────────────────

    private void LoadVisionSettings()
    {
        var v = _settings.Current.Vision;
        VisionEnabled = v.Enabled;
        VisionDefaultQuery = v.DefaultQuery;
        VisionAutoRecordQuery = v.AutoRecordQuery;
        VisionMaxResponseTokens = v.MaxResponseTokens;
        VisionMaxImageDimensionPx = v.MaxImageDimensionPx;
        VisionOutputModeIndex = Array.IndexOf(OutputModeCodes, v.OutputMode) is var oi and >= 0 ? oi : 0;
        IsVisionCloudTab = !string.Equals(v.VisionProvider, "ollama", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void SaveVision()
    {
        string outputMode = VisionOutputModeIndex >= 0 && VisionOutputModeIndex < OutputModeCodes.Length
            ? OutputModeCodes[VisionOutputModeIndex] : "inject";

        var updated = _settings.Current with
        {
            Vision = _settings.Current.Vision with
            {
                Enabled = VisionEnabled,
                DefaultQuery = VisionDefaultQuery,
                AutoRecordQuery = VisionAutoRecordQuery,
                MaxResponseTokens = VisionMaxResponseTokens,
                MaxImageDimensionPx = VisionMaxImageDimensionPx,
                OutputMode = outputMode,
            },
        };

        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save Vision pipeline settings");
            }
        }, TaskScheduler.Default);
    }
}
