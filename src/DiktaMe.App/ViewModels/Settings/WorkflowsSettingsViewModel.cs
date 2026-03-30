
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using Serilog;
using Windows.Storage.Pickers;

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
    private bool _clipInjectAtCursor = true;

    [ObservableProperty]
    private bool _ocrInjectAtCursor = true;

    [ObservableProperty]
    private bool _colorPickerInjectAtCursor = true;

    [ObservableProperty]
    private bool _videoAiInjectAtCursor = true;

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

    // Action prompts
    [ObservableProperty]
    private string _ocrPrompt = "";

    [ObservableProperty]
    private string _tablePrompt = "";

    [ObservableProperty]
    private string _videoDescribePrompt = "";

    [ObservableProperty]
    private string _videoDocumentPrompt = "";

    [ObservableProperty]
    private string _videoBugReportPrompt = "";

    [ObservableProperty]
    private string _videoSystemPrompt = "";

    // Save folder
    [ObservableProperty]
    private string _visionSaveFolder = "";

    // ── Screen recording settings ────────────────────────────────────────

    [ObservableProperty]
    private int _videoQualityIndex = 1; // 0=Low, 1=Medium, 2=High

    [ObservableProperty]
    private bool _enableMicAudio = true;

    [ObservableProperty]
    private bool _enableSystemAudio = true;

    [ObservableProperty]
    private bool _enableWebcam = true;

    [ObservableProperty]
    private int _webcamSizeIndex = 1; // 0=Small(150), 1=Medium(200), 2=Large(300), 3=XL(400)

    [ObservableProperty]
    private int _webcamPositionIndex; // 0=BottomRight, 1=BottomLeft, 2=TopRight, 3=TopLeft

    // Audio device pickers
    [ObservableProperty]
    private string _selectedMicDevice = "";

    [ObservableProperty]
    private string _selectedOutputDevice = "";

    public ObservableCollection<string> MicDevices { get; } = [];
    public ObservableCollection<string> OutputDevices { get; } = [];

    public string[] VideoQualityOptions => ["Low (1.5 Mbps, 24 fps)", "Medium (5 Mbps, 30 fps)", "High (8 Mbps, 60 fps)"];
    private static readonly string[] VideoQualityCodes = ["low", "medium", "high"];

    public string[] WebcamSizeOptions => ["Small (150px)", "Medium (200px)", "Large (300px)", "XL (400px)"];
    private static readonly int[] WebcamSizeValues = [150, 200, 300, 400];

    public string[] WebcamPositionOptions => ["Bottom Right", "Bottom Left", "Top Right", "Top Left"];
    private static readonly string[] WebcamPositionCodes = ["bottom-right", "bottom-left", "top-right", "top-left"];


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
        VisionMaxResponseTokens = v.MaxResponseTokens;
        VisionMaxImageDimensionPx = v.MaxImageDimensionPx;
        IsVisionCloudTab = !string.Equals(v.VisionProvider, "ollama", StringComparison.OrdinalIgnoreCase);

        // Inject-at-cursor toggles
        ClipInjectAtCursor = v.ClipInjectAtCursor;
        OcrInjectAtCursor = v.OcrInjectAtCursor;
        ColorPickerInjectAtCursor = v.ColorPickerInjectAtCursor;
        VideoAiInjectAtCursor = v.VideoAiInjectAtCursor;

        // System prompts
        VisionCloudSystemPrompt = v.CloudSystemPrompt;
        VisionLocalSystemPrompt = v.LocalSystemPrompt;

        // Action prompts
        OcrPrompt = v.OcrPrompt;
        TablePrompt = v.TablePrompt;
        VideoDescribePrompt = v.VideoDescribePrompt;
        VideoDocumentPrompt = v.VideoDocumentPrompt;
        VideoBugReportPrompt = v.VideoBugReportPrompt;
        VideoSystemPrompt = v.VideoSystemPrompt;

        // Save folder
        VisionSaveFolder = v.SaveFolder;

        // Screen recording settings
        VideoQualityIndex = Array.IndexOf(VideoQualityCodes, v.VideoQuality) is var qi and >= 0 ? qi : 1;
        EnableMicAudio = v.EnableMicAudio;
        EnableSystemAudio = v.EnableSystemAudio;
        EnableWebcam = v.EnableWebcam;
        WebcamSizeIndex = Array.IndexOf(WebcamSizeValues, v.WebcamSize) is var si and >= 0 ? si : 1;
        WebcamPositionIndex = Array.IndexOf(WebcamPositionCodes, v.WebcamPosition) is var pi and >= 0 ? pi : 0;

        // Audio devices
        LoadAudioDevices();
        SelectedMicDevice = string.IsNullOrEmpty(v.MicDeviceName) ? "Default" : v.MicDeviceName;
        SelectedOutputDevice = string.IsNullOrEmpty(v.SystemAudioDeviceName) ? "Default" : v.SystemAudioDeviceName;
    }

    private void LoadAudioDevices()
    {
        MicDevices.Clear();
        MicDevices.Add("Default");
        try
        {
            foreach (var device in AudioDeviceManager.GetInputDevices())
            {
                MicDevices.Add(device.Name);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate mic devices");
        }

        OutputDevices.Clear();
        OutputDevices.Add("Default");
        try
        {
            foreach (var device in AudioDeviceManager.GetOutputDevices())
            {
                OutputDevices.Add(device.Name);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to enumerate output devices");
        }
    }

    [RelayCommand]
    private void SaveVision()
    {
        string videoQuality = VideoQualityIndex >= 0 && VideoQualityIndex < VideoQualityCodes.Length
            ? VideoQualityCodes[VideoQualityIndex] : "medium";
        int webcamSize = WebcamSizeIndex >= 0 && WebcamSizeIndex < WebcamSizeValues.Length
            ? WebcamSizeValues[WebcamSizeIndex] : 200;
        string webcamPosition = WebcamPositionIndex >= 0 && WebcamPositionIndex < WebcamPositionCodes.Length
            ? WebcamPositionCodes[WebcamPositionIndex] : "bottom-right";

        string micDevice = string.Equals(SelectedMicDevice, "Default", StringComparison.Ordinal) ? "" : SelectedMicDevice;
        string outputDevice = string.Equals(SelectedOutputDevice, "Default", StringComparison.Ordinal) ? "" : SelectedOutputDevice;

        var updated = _settings.Current with
        {
            Vision = _settings.Current.Vision with
            {
                Enabled = VisionEnabled,
                DefaultQuery = VisionDefaultQuery,
                MaxResponseTokens = VisionMaxResponseTokens,
                MaxImageDimensionPx = VisionMaxImageDimensionPx,
                ClipInjectAtCursor = ClipInjectAtCursor,
                OcrInjectAtCursor = OcrInjectAtCursor,
                ColorPickerInjectAtCursor = ColorPickerInjectAtCursor,
                VideoAiInjectAtCursor = VideoAiInjectAtCursor,
                VideoQuality = videoQuality,
                EnableWebcam = EnableWebcam,
                WebcamSize = webcamSize,
                WebcamPosition = webcamPosition,
                EnableMicAudio = EnableMicAudio,
                EnableSystemAudio = EnableSystemAudio,
                MicDeviceName = micDevice,
                SystemAudioDeviceName = outputDevice,
                SaveFolder = VisionSaveFolder,
                CloudSystemPrompt = VisionCloudSystemPrompt,
                LocalSystemPrompt = VisionLocalSystemPrompt,
                OcrPrompt = OcrPrompt,
                TablePrompt = TablePrompt,
                VideoDescribePrompt = VideoDescribePrompt,
                VideoDocumentPrompt = VideoDocumentPrompt,
                VideoBugReportPrompt = VideoBugReportPrompt,
                VideoSystemPrompt = VideoSystemPrompt,
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

    [RelayCommand]
    private async Task BrowseVisionFolderAsync()
    {
        try
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");

            // WinUI 3 requires HWND initialization
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null)
            {
                VisionSaveFolder = folder.Path;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to browse for vision save folder");
        }
    }
}
