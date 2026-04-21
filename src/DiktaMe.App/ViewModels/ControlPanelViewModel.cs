
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Pipeline;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Serilog;

namespace DiktaMe.App.ViewModels;

/// <summary>
/// Helper record for binding dictation modes to UI.
/// Includes pre-computed visual properties for active/inactive state.
/// </summary>
public sealed record DictationModeItem(
    string Id,
    string Title,
    string Subtitle,
    bool IsActive,
    string BackgroundHex,
    string ForegroundHex,
    string HoverBgHex,
    string HoverFgHex,
    string PressedBgHex,
    string HoverBorderHex,
    string PressedBorderHex);

/// <summary>
/// Refine mode selection (Auto = no audio, Voice = instruction audio).
/// </summary>
public enum RefineMode
{
    Auto,    // Refine Auto (text selection only, no audio)
    Voice,   // Refine Instruction (records audio for spoken command)
}

/// <summary>
/// Event args for post-capture action selection from the CP vision row.
/// </summary>
public sealed record VisionActionChosenEventArgs(
    DiktaMe.Core.Vision.VisionAction Action,
    string? UserQuery,
    bool UseLocal,
    bool SkipAi = false);

/// <summary>
/// Wizard step for the vision row in the CP bar (see SPEC_002 §21.4).
/// Controls which panel is visible in the single-row wizard.
/// </summary>
public enum VisionWizardStep
{
    /// <summary>Wizard inactive — vision row hidden.</summary>
    None,

    /// <summary>Step 1: User picks capture type — Image / Video / Color.</summary>
    CaptureType,

    /// <summary>Step 2: User picks capture mode — Region / Full Screen.</summary>
    CaptureMode,

    /// <summary>Step 2b: Video recording in progress — timer + pause + stop.</summary>
    Recording,

    /// <summary>Step 3: Post-capture actions — Save / Clipboard / Chat / Note / OCR / Table / Edit.</summary>
    PostCapture,

    /// <summary>Step 4: Query input + provider toggle + Go button.</summary>
    Query,
}

/// <summary>
/// Event args for vision capture requests from CP bar buttons.
/// </summary>
public enum VisionCaptureType
{
    ScreenshotRegion,
    ScreenshotFull,
    VideoRegion,
    VideoFull,
    ColorPick,
}

/// <summary>
/// ViewModel for the Control Panel (HUD dashboard).
/// Displays real-time pipeline state, session metrics, quick action toggles, and provider badges.
/// </summary>
public sealed partial class ControlPanelViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    internal SettingsManager Settings => _settings;
    private readonly DictationModeManager _dictationModes;
    private readonly AudioRecorder _recorder;
    private readonly MetricsCollector _metrics;
    private readonly LocalizationService _loc;
    private readonly ThemeService _themeService;
    private readonly DispatcherQueue _dispatcher;

    // Badge indicator brushes (local/cloud/off) — theme-aware, updated on theme change
    private readonly SolidColorBrush _badgeLocalBrush;
    private readonly SolidColorBrush _badgeCloudBrush;
    private readonly SolidColorBrush _badgeOffBrush;

    // Midnight preset colors (V1 palette — kept as-is for visual continuity)
    private const string MidnightActiveBg = "#FF00607A";
    private const string MidnightActiveFg = "#FFFFFFFF";
    private const string MidnightActiveHoverBg = "#FF00506A";
    private const string MidnightActivePressedBg = "#FF003A4D";
    private const string MidnightActiveHoverBorder = "#FF00709A";
    private const string MidnightActivePressedBorder = "#FF004052";
    private const string MidnightInactiveBg = "#FF00303D";
    private const string MidnightInactiveFg = "#FF888888";
    private const string MidnightInactiveHoverBg = "#FF003D4D";
    private const string MidnightInactivePressedBg = "#FF002530";
    private const string MidnightInactiveHoverBorder = "#FF004D66";
    private const string MidnightInactivePressedBorder = "#FF002030";

    private static string ToHex(Windows.UI.Color c) =>
        $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private static Windows.UI.Color ShiftColor(Windows.UI.Color c, int amount)
    {
        static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
        return Windows.UI.Color.FromArgb(c.A, Clamp(c.R + amount), Clamp(c.G + amount), Clamp(c.B + amount));
    }

    // ── Pipeline state ──────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the pipeline state changes. Used by LocalApiServer to broadcast state over IPC.
    /// Fires on the UI thread (inside the DispatcherQueue callback).
    /// </summary>
    public event EventHandler<PipelineState>? ExternalStateChanged;

    [ObservableProperty]
    private PipelineState _currentState = PipelineState.Idle;

    [ObservableProperty]
    private string _statusText = "";

    // ── Dictation modes (CRUD system) ───────────────────────────────────────

    /// <summary>Dynamic list of available dictation presets (user-created).</summary>
    [ObservableProperty]
    private ObservableCollection<DictationModeItem> _availableModes = [];

    /// <summary>ID of the currently active dictation mode (persisted in settings).</summary>
    [ObservableProperty]
    private string? _activeDictationModeId;

    /// <summary>Whether LLM is disabled (backward-compat for pipeline option construction).</summary>
    public bool IsLlmOff => LlmMode == LlmMode.Off;

    /// <summary>Display-only: Title of the currently active mode (for XAML compatibility).</summary>
    public string ActiveMode => _dictationModes.GetAllModes()
        .FirstOrDefault(m => string.Equals(m.Id, ActiveDictationModeId, StringComparison.Ordinal))?.Title ?? _loc.GetString("ControlPanel_Mode_Standard");

    // ── Session stats ───────────────────────────────────────────────────────

    [ObservableProperty]
    private int _requestCount;

    [ObservableProperty]
    private int _charCount;

    [ObservableProperty]
    private int _wordCount;

    [ObservableProperty]
    private double _wordsPerMinute;

    /// <summary>WORD/MIN display string ("--" when 0, otherwise rounded value).</summary>
    public string WordsPerMinuteFormatted => WordsPerMinute > 0
        ? ((int)WordsPerMinute).ToString(CultureInfo.InvariantCulture)
        : "--";

    // ── Performance stats (last pipeline run) ───────────────────────────────

    [ObservableProperty]
    private long _lastTotalMs;

    [ObservableProperty]
    private long _lastRecordingMs;

    [ObservableProperty]
    private long _lastTranscriptionMs;

    [ObservableProperty]
    private long _lastProcessingMs;

    [ObservableProperty]
    private long _lastInjectionMs;

    private bool _hasPerfData;

    /// <summary>Formatted total time (seconds).</summary>
    public string LastTotalFormatted => _hasPerfData ? FormatMs(LastTotalMs) : "--";

    /// <summary>Formatted recording time (seconds).</summary>
    public string LastRecordingFormatted => _hasPerfData ? FormatMs(LastRecordingMs) : "--";

    /// <summary>Formatted transcription time (seconds).</summary>
    public string LastTranscriptionFormatted => _hasPerfData ? FormatMs(LastTranscriptionMs) : "--";

    /// <summary>Formatted processing time (seconds).</summary>
    public string LastProcessingFormatted => _hasPerfData ? FormatMs(LastProcessingMs) : "--";

    /// <summary>Formatted injection time (seconds).</summary>
    public string LastInjectionFormatted => _hasPerfData ? FormatMs(LastInjectionMs) : "--";

    // ── Provider indicator badges ─────────────────────────────────────────

    [ObservableProperty]
    private string _sttProviderName = "--";

    [ObservableProperty]
    private string _llmProviderName = "--";

    [ObservableProperty]
    private string _ttsProviderName = "--";

    [ObservableProperty]
    private SolidColorBrush _sttBadgeBrush = null!; // set in constructor

    [ObservableProperty]
    private SolidColorBrush _llmBadgeBrush = null!; // set in constructor

    [ObservableProperty]
    private SolidColorBrush _ttsBadgeBrush = null!; // set in constructor

    private bool _suppressSave;

    // ── Quick action toggles ────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isSoundEnabled = true;

    [ObservableProperty]
    private bool _isLocalStt;

    [ObservableProperty]
    private LlmMode _llmMode = LlmMode.Cloud;

    /// <summary>Additional key value: "" (off), "Enter", or "Tab".</summary>
    [ObservableProperty]
    private string _additionalKeyValue = string.Empty;

    [ObservableProperty]
    private RefineMode _refineMode = RefineMode.Voice;

    /// <summary>Whether the refine toggle is on (Voice mode) or off (Auto mode).</summary>
    [ObservableProperty]
    private bool _isRefineVoice = true;

    [ObservableProperty]
    private TtsMode _ttsMode;

    [ObservableProperty]
    private bool _isLocalVision;

    // ── Toggle state labels ──────────────────────────────────────────────

    public string SoundStateLabel => IsSoundEnabled ? _loc.GetString("ControlPanel_State_On") : _loc.GetString("ControlPanel_State_Off");
    public string SttStateLabel => IsLocalStt ? _loc.GetString("ControlPanel_State_Local") : _loc.GetString("ControlPanel_State_Cloud");
    public string LlmStateLabel => LlmMode switch
    {
        LlmMode.Local => _loc.GetString("ControlPanel_State_Local"),
        LlmMode.Cloud => _loc.GetString("ControlPanel_State_Cloud"),
        LlmMode.Off => _loc.GetString("ControlPanel_State_Off"),
        _ => _loc.GetString("ControlPanel_State_Cloud"),
    };
    public string KeyStateLabel => AdditionalKeyValue switch
    {
        "Enter" => "Enter",
        "Tab" => "Tab",
        _ => _loc.GetString("ControlPanel_State_Off"),
    };
    public string RefineStateLabel => RefineMode == RefineMode.Voice ? _loc.GetString("ControlPanel_Refine_Voice_Short") : _loc.GetString("ControlPanel_Refine_Auto_Short");
    public string VisionStateLabel => IsLocalVision ? _loc.GetString("ControlPanel_State_Local") : _loc.GetString("ControlPanel_State_Cloud");
    public bool ShowVisionButton => _settings.Current.Vision.Enabled;
    public string TtsStateLabel => TtsMode switch
    {
        TtsMode.Local => _loc.GetString("ControlPanel_State_Local"),
        TtsMode.Cloud => _loc.GetString("ControlPanel_State_Cloud"),
        TtsMode.Off => _loc.GetString("ControlPanel_State_Off"),
        _ => _loc.GetString("ControlPanel_State_Off"),
    };

    // ── Tooltip localization strings ────────────────────────────────────────

    public string TooltipBadgeStt => _loc.GetString("Settings_AIEngine_Sub_Stt");
    public string TooltipBadgeLlm => _loc.GetString("Settings_AIEngine_Sub_Llm");
    public string TooltipBadgeTts => _loc.GetString("Settings_AIEngine_Sub_Tts");
    public string TooltipReq => _loc.GetString("ControlPanel_Tooltip_Req");
    public string TooltipChar => _loc.GetString("ControlPanel_Tooltip_Char");
    public string TooltipWords => _loc.GetString("ControlPanel_Tooltip_Words");
    public string TooltipWordMin => _loc.GetString("ControlPanel_Tooltip_WordMin");
    public string TooltipRec => _loc.GetString("ControlPanel_Tooltip_Rec");
    public string TooltipTrns => _loc.GetString("ControlPanel_Tooltip_Trns");
    public string TooltipProc => _loc.GetString("ControlPanel_Tooltip_Proc");
    public string TooltipInj => _loc.GetString("ControlPanel_Tooltip_Inj");
    public string TooltipTot => _loc.GetString("ControlPanel_Tooltip_Tot");


    // ── Toggle state color brushes (for cycle buttons) ──────────────────

    public SolidColorBrush SttStateBrush => IsLocalStt ? _badgeLocalBrush : _badgeCloudBrush;
    public SolidColorBrush LlmStateBrush => LlmMode switch
    {
        LlmMode.Local => _badgeLocalBrush,
        LlmMode.Cloud => _badgeCloudBrush,
        LlmMode.Off => _badgeOffBrush,
        _ => _badgeCloudBrush,
    };
    public SolidColorBrush TtsStateBrush => TtsMode switch
    {
        TtsMode.Local => _badgeLocalBrush,
        TtsMode.Cloud => _badgeCloudBrush,
        TtsMode.Off => _badgeOffBrush,
        _ => _badgeOffBrush,
    };
    public SolidColorBrush VisionStateBrush => IsLocalVision ? _badgeLocalBrush : _badgeCloudBrush;
    public SolidColorBrush SoundStateBrush => IsSoundEnabled ? _badgeLocalBrush : _badgeOffBrush;
    public SolidColorBrush KeyStateBrush => string.IsNullOrEmpty(AdditionalKeyValue) ? _badgeOffBrush : _badgeLocalBrush;
    public SolidColorBrush RefineStateBrush => IsRefineVoice ? _badgeLocalBrush : _badgeCloudBrush;

    // ── Hotkey display ────────────────────────────────────────────────────

    [ObservableProperty]
    private string _hotkeyDictate = "Ctrl+Alt+D";

    [ObservableProperty]
    private string _hotkeyRefine = "Ctrl+Alt+R";

    [ObservableProperty]
    private string _hotkeyAsk = "Ctrl+Alt+A";

    [ObservableProperty]
    private string _hotkeyNote = "Ctrl+Alt+N";

    [ObservableProperty]
    private string _hotkeyTranslate = "Ctrl+Alt+T";

    // ── Row visibility (from ControlPanelSettings) ──────────────────────────

    [ObservableProperty]
    private bool _showModesRow = true;

    [ObservableProperty]
    private bool _showActionsRow = true;

    [ObservableProperty]
    private bool _showSessionStats = true;

    [ObservableProperty]
    private bool _showPerformanceStats = true;

    // ── Collapse/expand + always-on-top ───────────────────────────────────

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _expandUpward;

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private string _barPosition = "TopRight";

    // ── Visual effects settings (read-only, consumed by code-behind timer) ──

    [ObservableProperty]
    private bool _visualEffectsEnabled = true;

    [ObservableProperty]
    private bool _visualEffectsWholeApp = true;

    [ObservableProperty]
    private double _visualEffectsIntensity = 0.5;

    // ── Auto-collapse settings (read-only, consumed by code-behind timer) ─

    [ObservableProperty]
    private bool _autoCollapseEnabled;

    [ObservableProperty]
    private int _autoCollapseDelaySeconds = 10;

    // ── Waveform style (read-only, consumed by code-behind timer) ────────

    [ObservableProperty]
    private string _waveformStyle = "Wave";

    // ── Auto-hide settings (read-only, consumed by code-behind timer) ───

    [ObservableProperty]
    private bool _autoHideEnabled;

    [ObservableProperty]
    private int _autoHideDelaySeconds = 30;

    // ── Idle roll animation (read-only, consumed by code-behind timer) ──

    [ObservableProperty]
    private bool _idleRollEnabled = true;

    [ObservableProperty]
    private bool _idleRollShowClock = true;

    [ObservableProperty]
    private bool _idleRollShowWeather = true;

    [ObservableProperty]
    private string _idleRollClockFormat = "ddd M/d HH:mm";

    [ObservableProperty]
    private int _idleRollHoldSeconds = 5;

    [ObservableProperty]
    private string _weatherText = "";

    [ObservableProperty]
    private string _weatherIcon = "";

    // ── Wallet balance HUD ──────────────────────────────────────────────

    [ObservableProperty]
    private string _walletBalanceFormatted = "";

    [ObservableProperty]
    private bool _showWalletBalance;

    /// <summary>Effective row visibility: combines IsExpanded with per-row settings.</summary>
    public bool ShowModesRowEffective => IsExpanded && ShowModesRow;
    public bool ShowActionsRowEffective => IsExpanded && ShowActionsRow;
    public bool ShowSessionStatsEffective => IsExpanded && ShowSessionStats;
    public bool ShowPerformanceStatsEffective => IsExpanded && ShowPerformanceStats;
    public bool ShowFooterEffective => IsExpanded;

    // ── Vision Row state ─────────────────────────────────────────────────

    /// <summary>Raised when user clicks a pre-capture button (screenshot, video, color).</summary>
    public event EventHandler<VisionCaptureType>? VisionCaptureRequested;

    /// <summary>Raised when user picks a post-capture action (save, clipboard, chat, etc.).</summary>
    public event EventHandler<VisionActionChosenEventArgs>? VisionActionChosen;

    /// <summary>Raised when user clicks Stop during video recording.</summary>
    public event EventHandler? RecordingStopRequested;

    /// <summary>Raised when user clicks Pause/Resume during video recording.</summary>
    public event EventHandler? RecordingPauseToggleRequested;

    /// <summary>Fired when vision wizard exits (for dim overlay cleanup).</summary>
    public event EventHandler? VisionExited;

    /// <summary>Fired when user clicks a Step 1 button, overriding the default-to-region selection.</summary>
    public event EventHandler? VisionDefaultOverridden;

    [ObservableProperty]
    private VisionWizardStep _visionPhase = VisionWizardStep.None;

    /// <summary>Vision row is visible whenever phase is not None — ignores IsExpanded.</summary>
    public bool ShowVisionRow => VisionPhase != VisionWizardStep.None;

    /// <summary>Pre-capture buttons visible.</summary>
    /// <summary>Step 1: Capture type selection (Image/Video/Color).</summary>
    public bool IsStep_CaptureType => VisionPhase == VisionWizardStep.CaptureType;

    /// <summary>Step 2: Capture mode selection (Region/Full Screen).</summary>
    public bool IsStep_CaptureMode => VisionPhase == VisionWizardStep.CaptureMode;

    /// <summary>Step 2b: Video recording in progress (timer + pause + stop).</summary>
    public bool IsStep_Recording => VisionPhase == VisionWizardStep.Recording;

    /// <summary>Step 3: Post-capture actions.</summary>
    public bool IsStep_PostCapture => VisionPhase == VisionWizardStep.PostCapture;

    /// <summary>Step 4: Query input + provider + Go.</summary>
    public bool IsStep_Query => VisionPhase == VisionWizardStep.Query;

    /// <summary>Suppress auto-collapse while vision flow is active.</summary>
    public bool SuppressAutoCollapse => VisionPhase != VisionWizardStep.None;

    [ObservableProperty]
    private string _visionQueryText = "";

    /// <summary>True while AI is processing a vision request. Shows spinner, disables buttons.</summary>
    [ObservableProperty]
    private bool _isVisionProcessing;

    /// <summary>True when PostCapture shows video actions (Describe/Document/BugReport) instead of image actions.</summary>
    [ObservableProperty]
    private bool _isVideoPostCapture;

    /// <summary>AI provider mode for vision: 0=Local, 1=Cloud, 2=None.</summary>
    [ObservableProperty]
    private int _visionAiMode = 2; // Default: None (no AI)

    /// <summary>Show query sub-row (when post-capture action needs AI input).</summary>
    [ObservableProperty]
    private bool _showVisionQueryRow;

    /// <summary>Captured image thumbnail for post-capture preview.</summary>
    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource? _visionThumbnail;

    /// <summary>Snapshot of IsExpanded before vision mode activated — restored on exit.</summary>
    private bool _preVisionIsExpanded;

    /// <summary>Pending post-capture action (set when user clicks action that needs query input).</summary>
    private DiktaMe.Core.Vision.VisionAction? _pendingVisionAction;

    /// <summary>Video recording elapsed time display.</summary>
    [ObservableProperty]
    private string _recordingTimerText = "00:00";

    /// <summary>Whether video recording is paused.</summary>
    [ObservableProperty]
    private bool _isRecordingPaused;

    partial void OnVisionPhaseChanged(VisionWizardStep value)
    {
        OnPropertyChanged(nameof(ShowVisionRow));
        OnPropertyChanged(nameof(IsStep_CaptureType));
        OnPropertyChanged(nameof(IsStep_CaptureMode));
        OnPropertyChanged(nameof(IsStep_Recording));
        OnPropertyChanged(nameof(IsStep_PostCapture));
        OnPropertyChanged(nameof(IsStep_Query));
        OnPropertyChanged(nameof(SuppressAutoCollapse));

        // Reset query text when leaving query step
        if (value != VisionWizardStep.Query)
        {
            VisionQueryText = "";
        }

        Log.Information("VisionRow: Phase → {Phase}", value);
    }

    // ── Vision Row commands ──────────────────────────────────────────────

    // Old individual capture commands removed — replaced by Step 1/2 wizard flow

    /// <summary>Enter vision mode — snapshots current CP state for restore on exit.</summary>
    public void EnterVision()
    {
        _preVisionIsExpanded = IsExpanded;
        IsExpanded = false; // Force collapse so only Header + Vision row show
        VisionAiMode = 2; // Always default to "None" (no AI) on each wizard entry
        VisionPhase = VisionWizardStep.CaptureType;
        Log.Information("VisionRow: Entered vision mode (was expanded={WasExpanded})", _preVisionIsExpanded);
    }

    [RelayCommand]
    private void VisionExitMode()
    {
        var wasExpanded = _preVisionIsExpanded;
        VisionPhase = VisionWizardStep.None;
        VisionThumbnail = null;
        IsVideoPostCapture = false;
        IsVisionProcessing = false;

        // Restore pre-vision expand state
        if (IsExpanded != wasExpanded)
        {
            IsExpanded = wasExpanded;
        }

        VisionExited?.Invoke(this, EventArgs.Empty);

        Log.Information("VisionRow: Exited vision mode (restored expanded={Expanded})", wasExpanded);
    }

    // ── Step 1: Capture Type selection ─────────────────────────────────

    /// <summary>Tracks whether user chose Image or Video at Step 1 (affects Step 2 options).</summary>
    private bool _captureTypeIsVideo; // false = image, true = video

    [RelayCommand]
    private void VisionSelectImage()
    {
        VisionDefaultOverridden?.Invoke(this, EventArgs.Empty);
        _captureTypeIsVideo = false;
        VisionPhase = VisionWizardStep.CaptureMode;
    }

    [RelayCommand]
    private void VisionSelectVideo()
    {
        VisionDefaultOverridden?.Invoke(this, EventArgs.Empty);
        _captureTypeIsVideo = true;
        VisionPhase = VisionWizardStep.CaptureMode;
    }

    [RelayCommand]
    private void VisionSelectColor()
    {
        VisionDefaultOverridden?.Invoke(this, EventArgs.Empty);
        VisionCaptureRequested?.Invoke(this, VisionCaptureType.ColorPick);
    }

    /// <summary>Whether Step 2 is showing Image options (Region/Full) vs Video options.</summary>
    public bool IsCaptureModeImage => !_captureTypeIsVideo;

    // ── Step 2: Capture Mode selection ──────────────────────────────────

    [RelayCommand]
    private void VisionCaptureRegion()
    {
        var type = _captureTypeIsVideo ? VisionCaptureType.VideoRegion : VisionCaptureType.ScreenshotRegion;
        VisionCaptureRequested?.Invoke(this, type);
    }

    [RelayCommand]
    private void VisionCaptureFull()
    {
        var type = _captureTypeIsVideo ? VisionCaptureType.VideoFull : VisionCaptureType.ScreenshotFull;
        VisionCaptureRequested?.Invoke(this, type);
    }

    // ── Step 3: Post-Capture action selection ───────────────────────────

    [RelayCommand]
    private void VisionChooseAction(string actionName)
    {
        if (!Enum.TryParse<DiktaMe.Core.Vision.VisionAction>(actionName, out var action))
            return;

        // Save — fire immediately, no AI
        if (action == DiktaMe.Core.Vision.VisionAction.Save)
        {
            VisionActionChosen?.Invoke(this, new VisionActionChosenEventArgs(action, null, false, true));
            return;
        }

        // OCR and Table — auto-run with current provider, no query needed
        if (action == DiktaMe.Core.Vision.VisionAction.Ocr || action == DiktaMe.Core.Vision.VisionAction.Table)
        {
            var useLocal = VisionAiMode == 0;
            VisionActionChosen?.Invoke(this, new VisionActionChosenEventArgs(action, null, useLocal));
            return;
        }

        // Edit — fire immediately, will return to Query step after annotation
        if (action == DiktaMe.Core.Vision.VisionAction.Edit)
        {
            VisionActionChosen?.Invoke(this, new VisionActionChosenEventArgs(action, null, false));
            return;
        }

        // Clipboard, Chat, Note — need query input → slide to Step 4
        _pendingVisionAction = action;
        VisionPhase = VisionWizardStep.Query;
    }

    // ── Step 4: Query submission ────────────────────────────────────────

    [RelayCommand]
    private void VisionSubmitQuery()
    {
        if (_pendingVisionAction is not { } action)
            return;

        var useLocal = VisionAiMode == 0;
        var skipAi = VisionAiMode == 2; // "None" mode
        var query = string.IsNullOrWhiteSpace(VisionQueryText) ? null : VisionQueryText.Trim();

        _pendingVisionAction = null;

        VisionActionChosen?.Invoke(this, new VisionActionChosenEventArgs(action, query, useLocal, skipAi));
    }

    [RelayCommand]
    private void VisionRecordingStop() => RecordingStopRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void VisionRecordingPauseToggle()
    {
        IsRecordingPaused = !IsRecordingPaused;
        RecordingPauseToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Chevron icon reflecting expand direction. Points toward the direction content will collapse.</summary>
    public string ExpandCollapseIcon => ExpandUpward
        ? (IsExpanded ? "\uE70D" : "\uE70E")   // Up mode: down-chevron to collapse, up-chevron to expand
        : (IsExpanded ? "\uE70E" : "\uE70D");   // Down mode: up-chevron to collapse, down-chevron to expand

    public ControlPanelViewModel(
        SettingsManager settings,
        DictationModeManager dictationModes,
        AudioRecorder recorder,
        MetricsCollector metrics,
        LocalizationService loc,
        ThemeService themeService)
    {
        _settings = settings;
        _dictationModes = dictationModes;
        _recorder = recorder;
        _metrics = metrics;
        _loc = loc;
        _themeService = themeService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _statusText = _loc.GetString("ControlPanel_State_Ready");

        // Initialize badge brushes based on current theme
        bool isDark = ThemeService.GetPalette(_themeService.CurrentTheme).IsDark;
        _badgeLocalBrush = new SolidColorBrush(isDark
            ? ColorHelper.FromArgb(255, 122, 255, 158)   // #7AFF9E bright green (dark themes)
            : ColorHelper.FromArgb(255, 4, 120, 87));    // #047857 dark emerald (Frost)
        _badgeCloudBrush = new SolidColorBrush(isDark
            ? ColorHelper.FromArgb(255, 78, 168, 222)    // #4EA8DE bright blue (dark themes)
            : ColorHelper.FromArgb(255, 29, 78, 216));   // #1D4ED8 dark blue (Frost)
        _badgeOffBrush = new SolidColorBrush(isDark
            ? ColorHelper.FromArgb(255, 255, 68, 68)     // #FF4444 bright red (dark themes)
            : ColorHelper.FromArgb(255, 185, 28, 28));   // #B91C1C dark red (Frost)

        // Initialize observable badge brushes with defaults (updated by LoadFromSettings → UpdateBadgeBrushes)
        _sttBadgeBrush = _badgeCloudBrush;
        _llmBadgeBrush = _badgeCloudBrush;
        _ttsBadgeBrush = _badgeOffBrush;

        // Subscribe to recorder events
        _recorder.RecordingStarted += OnRecordingStarted;
        _recorder.RecordingStopped += OnRecordingStopped;

        // Subscribe to settings changes
        _settings.SettingsChanged += OnSettingsChanged;

        // Subscribe to theme changes for badge brush updates
        _themeService.ThemeChanged += OnThemeChanged;

        // Load initial state from settings (suppress saves to prevent premature settings.json creation)
        _suppressSave = true;
        LoadFromSettings(_settings.Current);
        _suppressSave = false;

        // Load available dictation modes (must happen synchronously in constructor)
        var modes = _dictationModes.GetAllModes();
        foreach (var mode in modes)
        {
            var profile = GetActiveProfile(mode);
            AvailableModes.Add(CreateModeItem(mode, profile));
        }

        // Default to first mode if none selected
        if (string.IsNullOrEmpty(ActiveDictationModeId) && modes.Count > 0)
        {
            ActiveDictationModeId = modes[0].Id;
        }

        Log.Debug("ControlPanel: Loaded {Count} dictation presets in constructor, active={ActiveId}",
            modes.Count, ActiveDictationModeId);
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void SetMode(string modeId)
    {
        if (string.IsNullOrEmpty(modeId))
        {
            return;
        }

        ActiveDictationModeId = modeId;

        // Persist to settings (fire-and-forget)
        var updated = _settings.Current with
        {
            ActiveDictationModeId = modeId
        };
        _ = _settings.UpdateAsync(updated);

        // Refresh UI (mark selected mode as active)
        LoadAvailableModes();

        // Notify XAML that ActiveMode display property changed
        OnPropertyChanged(nameof(ActiveMode));

        Log.Information("ControlPanel: Active dictation mode set to {ModeId}", modeId);
    }

    [RelayCommand]
    private void CycleVision() => IsLocalVision = !IsLocalVision;

    [RelayCommand]
    private void CycleStt() => IsLocalStt = !IsLocalStt;

    [RelayCommand]
    private void CycleLlm()
    {
        LlmMode = LlmMode switch
        {
            LlmMode.Local => LlmMode.Cloud,
            LlmMode.Cloud => LlmMode.Off,
            LlmMode.Off => LlmMode.Local,
            _ => LlmMode.Local,
        };
    }

    [RelayCommand]
    private void CycleTts()
    {
        TtsMode = TtsMode switch
        {
            TtsMode.Local => TtsMode.Cloud,
            TtsMode.Cloud => TtsMode.Off,
            TtsMode.Off => TtsMode.Local,
            _ => TtsMode.Local,
        };
    }

    [RelayCommand]
    private void CycleSound() => IsSoundEnabled = !IsSoundEnabled;

    [RelayCommand]
    private void CycleKey()
    {
        AdditionalKeyValue = AdditionalKeyValue switch
        {
            "" => "Enter",
            "Enter" => "Tab",
            _ => "",
        };
    }

    [RelayCommand]
    private void CycleRefine()
    {
        RefineMode = RefineMode == RefineMode.Auto ? RefineMode.Voice : RefineMode.Auto;
        IsRefineVoice = RefineMode == RefineMode.Voice;
    }

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void FlipExpandDirection() => ExpandUpward = !ExpandUpward;

    [RelayCommand]
    private void OpenSettings()
    {
        App.Current.ShowSettings();
    }

    [RelayCommand]
    private void CloseWindow()
    {
        // Triggers the AppWindow.Closing handler in App.xaml.cs which hides (not destroys) the window
        App.Current.HideMainWindow();
    }

    // ── Toggle handlers ─────────────────────────────────────────────────────

    partial void OnIsSoundEnabledChanged(bool value)
    {
        if (!_suppressSave)
        {
            var updated = _settings.Current with
            {
                General = _settings.Current.General with { SoundFeedback = value }
            };
            _ = _settings.UpdateAsync(updated);
        }

        OnPropertyChanged(nameof(SoundStateLabel));
        OnPropertyChanged(nameof(SoundStateBrush));
    }

    partial void OnIsLocalSttChanged(bool value)
    {
        if (!_suppressSave)
        {
            string sttProvider = value ? "whisper" : "deepgram";
            var profiles = new Dictionary<string, ModeSettings>(_settings.Current.ModeProfiles);
            string[] modes = ["dictate", "refine", "ask", "translate", "note", "chat"];
            foreach (var mode in modes)
            {
                for (int p = 0; p < 2; p++)
                {
                    string key = $"{mode}_{p}";
                    var existing = profiles.TryGetValue(key, out var ms) ? ms : new ModeSettings();
                    profiles[key] = existing with { SttProvider = sttProvider };
                }
            }

            var updated = _settings.Current with { ModeProfiles = profiles };
            _ = _settings.UpdateAsync(updated);
        }

        UpdateBadgeBrushes();
        OnPropertyChanged(nameof(SttStateLabel));
        OnPropertyChanged(nameof(SttStateBrush));
        Log.Information("ControlPanel: STT toggled to {Provider}", value ? "whisper" : "deepgram");
    }

    partial void OnIsLocalVisionChanged(bool value)
    {
        if (!_suppressSave)
        {
            var vision = _settings.Current.Vision;
            string provider = value ? "ollama" : vision.CloudVisionProvider;
            string model = value ? vision.LocalVisionModelId : vision.CloudVisionModelId;
            if (string.IsNullOrWhiteSpace(provider)) provider = value ? "ollama" : "gemini";
            if (string.IsNullOrWhiteSpace(model)) model = value ? "moondream" : "gemini-2.5-flash";

            var updated = _settings.Current with
            {
                Vision = vision with { VisionProvider = provider, VisionModelId = model },
            };
            _ = _settings.UpdateAsync(updated);
        }

        OnPropertyChanged(nameof(VisionStateLabel));
        OnPropertyChanged(nameof(VisionStateBrush));
        Log.Information("ControlPanel: Vision toggled to {Provider}", value ? "local" : "cloud");
    }

    partial void OnLlmModeChanged(LlmMode value)
    {
        if (!_suppressSave)
        {
            bool rawMode = value == LlmMode.Off;

            // The pill is a Cloud/Local/Off switch — it flips ActiveProfileName (which
            // selects CloudProfile vs LocalProfile in DictationModeManager) and toggles
            // UseLlm. It must NOT overwrite ModeProfiles[...].LlmProvider — doing so
            // clobbers the user's per-mode provider selection (BUG-030). The LLM provider
            // is now derived from DictationProfile.ModelName via LLMRouter, not stored here.
            var profiles = new Dictionary<string, ModeSettings>(_settings.Current.ModeProfiles);
            string[] modes = ["dictate", "refine", "ask", "translate", "note", "chat"];
            foreach (var mode in modes)
            {
                for (int p = 0; p < 2; p++)
                {
                    string key = $"{mode}_{p}";
                    var existing = profiles.TryGetValue(key, out var ms) ? ms : new ModeSettings();
                    profiles[key] = existing with { UseLlm = !rawMode };
                }
            }

            // Off preserves the prior ActiveProfileName — toggling LLM off shouldn't
            // silently change which profile (Cloud vs Local) future dictations use.
            string activeProfileName = value switch
            {
                LlmMode.Local => "Local",
                LlmMode.Cloud => "Cloud",
                _ => _settings.Current.ActiveProfileName,
            };

            var updated = _settings.Current with
            {
                General = _settings.Current.General with { RawModeOverride = rawMode },
                ModeProfiles = profiles,
                ActiveProfileName = activeProfileName,
            };
            _ = _settings.UpdateAsync(updated);
        }

        UpdateBadgeBrushes();
        OnPropertyChanged(nameof(LlmStateLabel));
        OnPropertyChanged(nameof(LlmStateBrush));
        OnPropertyChanged(nameof(IsLlmOff));

        // Refresh mode items (subtitle may change between Cloud/Local profile)
        LoadAvailableModes();

        Log.Information("ControlPanel: LLM mode set to {Mode}", value);
    }

    private void UpdateBadgeBrushes()
    {
        // STT: green = local (Whisper), blue = cloud (Deepgram). Never red.
        SttBadgeBrush = IsLocalStt ? _badgeLocalBrush : _badgeCloudBrush;

        // LLM: red = off (RAW mode), green = local (Ollama), blue = cloud
        switch (LlmMode)
        {
            case LlmMode.Off:
                LlmBadgeBrush = _badgeOffBrush;
                LlmProviderName = "Disabled (RAW)";
                break;
            case LlmMode.Local:
                LlmBadgeBrush = _badgeLocalBrush;
                break;
            case LlmMode.Cloud:
                LlmBadgeBrush = _badgeCloudBrush;
                break;
        }

        // TTS: red = off, green = local (Kokoro), blue = cloud
        switch (TtsMode)
        {
            case TtsMode.Off:
                TtsBadgeBrush = _badgeOffBrush;
                TtsProviderName = "Disabled";
                break;
            case TtsMode.Local:
                TtsBadgeBrush = _badgeLocalBrush;
                TtsProviderName = "Kokoro (local)";
                break;
            case TtsMode.Cloud:
                TtsBadgeBrush = _badgeCloudBrush;
                string provider = _settings.Current.Tts.Provider;
                TtsProviderName = provider.ToLowerInvariant() switch
                {
                    "deepgram" => "Deepgram Aura",
                    "inworld" => "Inworld TTS",
                    "openai" => "OpenAI TTS",
                    _ => provider,
                };
                break;
        }
    }

    partial void OnAdditionalKeyValueChanged(string value)
    {
        if (!_suppressSave)
        {
            var updated = _settings.Current with
            {
                General = _settings.Current.General with { AdditionalKey = value }
            };
            _ = _settings.UpdateAsync(updated);
        }

        OnPropertyChanged(nameof(KeyStateLabel));
        OnPropertyChanged(nameof(KeyStateBrush));
    }

    partial void OnIsRefineVoiceChanged(bool value)
    {
        RefineMode = value ? RefineMode.Voice : RefineMode.Auto;
        if (!_suppressSave)
        {
            var updated = _settings.Current with
            {
                General = _settings.Current.General with { RefineVoiceMode = value }
            };
            _ = _settings.UpdateAsync(updated);
        }

        OnPropertyChanged(nameof(RefineStateLabel));
        OnPropertyChanged(nameof(RefineStateBrush));
        Log.Information("ControlPanel: Refine mode set to {Mode}", RefineMode);
    }

    partial void OnTtsModeChanged(TtsMode value)
    {
        if (!_suppressSave)
        {
            bool enabled = value != TtsMode.Off;
            string provider = value switch
            {
                TtsMode.Local => "kokoro",
                TtsMode.Cloud => _settings.Current.Tts.Provider is "kokoro" or "" ? "deepgram" : _settings.Current.Tts.Provider,
                _ => _settings.Current.Tts.Provider,
            };

            var updated = _settings.Current with
            {
                Tts = _settings.Current.Tts with { Enabled = enabled, Provider = provider }
            };
            _ = _settings.UpdateAsync(updated);
        }

        OnPropertyChanged(nameof(TtsStateLabel));
        OnPropertyChanged(nameof(TtsStateBrush));
        UpdateBadgeBrushes();
        Log.Information("ControlPanel: TTS mode set to {Mode}", value);
    }

    partial void OnIsExpandedChanged(bool value)
    {
        if (!_suppressSave)
        {
            var updated = _settings.Current with
            {
                ControlPanel = _settings.Current.ControlPanel with { IsExpanded = value }
            };
            _ = _settings.UpdateAsync(updated);
        }

        OnPropertyChanged(nameof(ShowModesRowEffective));
        OnPropertyChanged(nameof(ShowActionsRowEffective));
        OnPropertyChanged(nameof(ShowSessionStatsEffective));
        OnPropertyChanged(nameof(ShowPerformanceStatsEffective));
        OnPropertyChanged(nameof(ShowFooterEffective));
        OnPropertyChanged(nameof(ExpandCollapseIcon));
        Log.Information("ControlPanel: Expanded set to {IsExpanded}", value);
    }

    partial void OnExpandUpwardChanged(bool value)
    {
        if (!_suppressSave)
        {
            var updated = _settings.Current with
            {
                ControlPanel = _settings.Current.ControlPanel with
                {
                    ExpandDirection = value ? "Up" : "Down"
                }
            };
            _ = _settings.UpdateAsync(updated);
        }

        OnPropertyChanged(nameof(ExpandCollapseIcon));
        Log.Information("ControlPanel: ExpandUpward set to {ExpandUpward}", value);
    }

    partial void OnBarPositionChanged(string value)
    {
        if (!_suppressSave)
        {
            var updated = _settings.Current with
            {
                ControlPanel = _settings.Current.ControlPanel with
                {
                    BarPosition = value,
                    WindowX = int.MinValue,
                    WindowY = int.MinValue
                }
            };
            _ = _settings.UpdateAsync(updated);

            // Auto-set expand direction based on position
            bool isBottom = value.StartsWith("Bottom", StringComparison.Ordinal);
            if (isBottom != ExpandUpward)
            {
                ExpandUpward = isBottom;
            }
        }

        Log.Information("ControlPanel: BarPosition set to {BarPosition}", value);
    }

    /// <summary>
    /// Force re-snap to current BarPosition even if the value hasn't changed.
    /// Called by settings page when user clicks the same snap button to reset after dragging.
    /// </summary>
    internal void ForceResnap()
    {
        // Clear saved pixel coords so snap takes effect
        var updated = _settings.Current with
        {
            ControlPanel = _settings.Current.ControlPanel with
            {
                WindowX = int.MinValue,
                WindowY = int.MinValue
            }
        };
        _ = _settings.UpdateAsync(updated);
        OnPropertyChanged(nameof(BarPosition));
    }

    partial void OnAlwaysOnTopChanged(bool value)
    {
        if (!_suppressSave)
        {
            var updated = _settings.Current with
            {
                ControlPanel = _settings.Current.ControlPanel with { AlwaysOnTop = value }
            };
            _ = _settings.UpdateAsync(updated);
        }

        // Apply to MainWindow presenter at runtime
        var window = App.Current.MainWindow;
        if (window?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
        {
            p.IsAlwaysOnTop = value;
        }

        Log.Information("ControlPanel: AlwaysOnTop set to {IsAlwaysOnTop}", value);
    }

    partial void OnShowModesRowChanged(bool value) => OnPropertyChanged(nameof(ShowModesRowEffective));
    partial void OnShowActionsRowChanged(bool value) => OnPropertyChanged(nameof(ShowActionsRowEffective));
    partial void OnShowSessionStatsChanged(bool value) => OnPropertyChanged(nameof(ShowSessionStatsEffective));
    partial void OnShowPerformanceStatsChanged(bool value) => OnPropertyChanged(nameof(ShowPerformanceStatsEffective));

    partial void OnWordsPerMinuteChanged(double value)
    {
        OnPropertyChanged(nameof(WordsPerMinuteFormatted));
    }

    // ── Public methods for pipeline event wiring ────────────────────────────

    /// <summary>
    /// Called by the app when a pipeline's state changes. Must be thread-safe.
    /// </summary>
    public void OnPipelineStateChanged(object? sender, PipelineState state)
    {
        _dispatcher.TryEnqueue(() =>
        {
            CurrentState = state;
            StatusText = state switch
            {
                PipelineState.Idle => _loc.GetString("ControlPanel_State_Ready"),
                PipelineState.Recording => _loc.GetString("ControlPanel_State_Listening"),
                PipelineState.Transcribing => _loc.GetString("ControlPanel_State_Transcribing"),
                PipelineState.Streaming => _loc.GetString("ControlPanel_State_Streaming"),
                PipelineState.Processing => _loc.GetString("ControlPanel_State_Thinking"),
                PipelineState.Injecting => _loc.GetString("ControlPanel_State_Typing"),
                PipelineState.Error => _loc.GetString("ControlPanel_State_Error"),
                _ => _loc.GetString("ControlPanel_State_Ready"),
            };
            ExternalStateChanged?.Invoke(this, state);
        });
    }

    /// <summary>
    /// Called by the app when a pipeline completes. Updates stats and performance metrics.
    /// </summary>
    public void OnPipelineCompleted(object? sender, PipelineResult result)
    {
        // Persist to SQLite history (privacy-gated by HistoryManager)
        _ = _metrics.RecordAsync(result);

        _dispatcher.TryEnqueue(() =>
        {
            // Mark that we have performance data
            _hasPerfData = true;

            // Update ALL performance stats (including TOT and REC for telemetry)
            LastTotalMs = result.TotalMs;
            LastRecordingMs = result.RecordingMs;
            LastTranscriptionMs = result.TranscriptionMs;
            LastProcessingMs = result.ProcessingMs;
            LastInjectionMs = result.InjectionMs;

            // Notify formatted properties
            OnPropertyChanged(nameof(LastTotalFormatted));
            OnPropertyChanged(nameof(LastRecordingFormatted));
            OnPropertyChanged(nameof(LastTranscriptionFormatted));
            OnPropertyChanged(nameof(LastProcessingFormatted));
            OnPropertyChanged(nameof(LastInjectionFormatted));

            // Update provider badges
            if (result.SttProvider is not null)
            {
                SttProviderName = result.SttProvider;
            }

            if (result.LlmProvider is not null)
            {
                LlmProviderName = result.LlmProvider;
            }

            // Reset state to idle after completion
            CurrentState = PipelineState.Idle;
            StatusText = _loc.GetString("ControlPanel_State_Ready");

            // Refresh session stats
            RefreshSessionStats(result);
        });
    }

    /// <summary>
    /// Refreshes session stats from the metrics collector.
    /// </summary>
    public void RefreshSessionStats(PipelineResult? lastResult = null)
    {
        var stats = _metrics.GetSessionStats();
        RequestCount = stats.Sessions;
        CharCount = stats.Chars;
        WordCount = stats.Words;

        // WPM = words dictated / wall-clock minutes (recording + STT + LLM + injection).
        // RecordingMs is the speaking time, TotalMs is the pipeline processing after recording.
        if (lastResult is not null && lastResult.WordCount > 0)
        {
            long wallClockMs = lastResult.RecordingMs + lastResult.TotalMs;
            if (wallClockMs > 0)
            {
                WordsPerMinute = Math.Round(lastResult.WordCount / (wallClockMs / 60_000.0), 1);
            }
        }
    }

    /// <summary>
    /// Refreshes weather data from the given service and updates display properties.
    /// Fire-and-forget safe — all errors are caught inside WeatherService.
    /// </summary>
    public async Task RefreshWeatherAsync(Core.Weather.WeatherService weatherService, CancellationToken cancellationToken)
    {
        var data = await weatherService.GetCurrentWeatherAsync(cancellationToken).ConfigureAwait(false);
        if (data is not null)
        {
            _dispatcher.TryEnqueue(() =>
            {
                WeatherIcon = data.Icon;
                WeatherText = string.IsNullOrEmpty(data.Region)
                    ? $"{data.TemperatureCelsius:F0}°C"
                    : $"{data.TemperatureCelsius:F0}°C · {data.Region}";
            });
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void OnRecordingStarted(object? sender, RecordingStartedEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            CurrentState = PipelineState.Recording;
            StatusText = _loc.GetString("ControlPanel_State_Listening");
        });
    }

    private void OnRecordingStopped(object? sender, RecordingStoppedEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (CurrentState == PipelineState.Recording)
            {
                CurrentState = PipelineState.Idle;
                StatusText = _loc.GetString("ControlPanel_State_Ready");
            }
        });
    }

    private void OnThemeChanged(object? sender, string themeName)
    {
        _dispatcher.TryEnqueue(() =>
        {
            try
            {
                bool isDark = ThemeService.GetPalette(themeName).IsDark;
                _badgeLocalBrush.Color = isDark
                    ? ColorHelper.FromArgb(255, 122, 255, 158)   // #7AFF9E
                    : ColorHelper.FromArgb(255, 4, 120, 87);     // #047857
                _badgeCloudBrush.Color = isDark
                    ? ColorHelper.FromArgb(255, 78, 168, 222)    // #4EA8DE
                    : ColorHelper.FromArgb(255, 29, 78, 216);    // #1D4ED8
                _badgeOffBrush.Color = isDark
                    ? ColorHelper.FromArgb(255, 255, 68, 68)     // #FF4444
                    : ColorHelper.FromArgb(255, 185, 28, 28);    // #B91C1C

                // Force re-evaluation of all badge brush properties
                UpdateBadgeBrushes();
                OnPropertyChanged(nameof(SttStateBrush));
                OnPropertyChanged(nameof(LlmStateBrush));
                OnPropertyChanged(nameof(TtsStateBrush));
                OnPropertyChanged(nameof(SoundStateBrush));
                OnPropertyChanged(nameof(KeyStateBrush));
                OnPropertyChanged(nameof(RefineStateBrush));

                // Refresh dictation preset buttons with new theme colors
                LoadAvailableModes();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ControlPanelViewModel: CRASH in OnThemeChanged");
            }
        });
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        _dispatcher.TryEnqueue(() =>
        {
            _suppressSave = true;
            LoadFromSettings(settings);
            _suppressSave = false;
        });
    }

    internal void LoadFromSettings(AppSettings settings)
    {
        IsSoundEnabled = settings.General.SoundFeedback;
        AdditionalKeyValue = settings.General.AdditionalKey;
        IsRefineVoice = settings.General.RefineVoiceMode;
        // Derive TtsMode from Tts.Enabled + Tts.Provider
        if (!settings.Tts.Enabled)
        {
            TtsMode = TtsMode.Off;
        }
        else
        {
            TtsMode = string.Equals(settings.Tts.Provider, "kokoro", StringComparison.OrdinalIgnoreCase)
                ? TtsMode.Local
                : TtsMode.Cloud;
        }
        // Read STT provider from ModeProfiles (dictate_0 is the reference slot)
        var refSlot = settings.ModeProfiles.GetValueOrDefault("dictate_0", new ModeSettings());
        IsLocalStt = string.Equals(refSlot.SttProvider, "whisper", StringComparison.OrdinalIgnoreCase);

        // Derive LlmMode from RawModeOverride + ActiveProfileName. ActiveProfileName is
        // the single source of truth for Cloud vs Local (consumed by DictationModeManager
        // .GetActiveProfile to pick CloudProfile vs LocalProfile). Deriving from the
        // deprecated ModeProfiles.LlmProvider field causes a feedback loop when the wizard
        // writes e.g. "anthropic" there and the pill writes ActiveProfileName="Local" —
        // LoadFromSettings then snaps LlmMode back to Cloud while the saved
        // ActiveProfileName stays "Local", silently routing dictation to the wrong profile.
        if (settings.General.RawModeOverride)
        {
            LlmMode = LlmMode.Off;
        }
        else
        {
            LlmMode = string.Equals(settings.ActiveProfileName, "Local", StringComparison.OrdinalIgnoreCase)
                ? LlmMode.Local
                : LlmMode.Cloud;
        }

        IsLocalVision = string.Equals(settings.Vision.VisionProvider, "ollama", StringComparison.OrdinalIgnoreCase);

        UpdateBadgeBrushes();

        ShowModesRow = settings.ControlPanel.ShowModesRow;
        ShowActionsRow = settings.ControlPanel.ShowActionsRow;
        ShowSessionStats = settings.ControlPanel.ShowSessionStats;
        ShowPerformanceStats = settings.ControlPanel.ShowPerformanceStats;
        AlwaysOnTop = settings.ControlPanel.AlwaysOnTop;
        IsExpanded = settings.ControlPanel.IsExpanded;
        ExpandUpward = string.Equals(settings.ControlPanel.ExpandDirection, "Up", StringComparison.Ordinal);
        VisualEffectsEnabled = settings.ControlPanel.VisualEffectsEnabled;
        VisualEffectsWholeApp = !string.Equals(settings.ControlPanel.VisualEffectsScope, "TopBarOnly", StringComparison.Ordinal);
        VisualEffectsIntensity = settings.ControlPanel.VisualEffectsIntensity;
        string newBarPos = settings.ControlPanel.BarPosition ?? "TopRight";
        BarPosition = newBarPos;
        AutoCollapseEnabled = settings.ControlPanel.AutoCollapseEnabled;
        AutoCollapseDelaySeconds = settings.ControlPanel.AutoCollapseDelaySeconds;
        WaveformStyle = settings.ControlPanel.WaveformStyle;
        AutoHideEnabled = settings.ControlPanel.AutoHideEnabled;
        AutoHideDelaySeconds = settings.ControlPanel.AutoHideDelaySeconds;
        IdleRollEnabled = settings.ControlPanel.IdleRollEnabled;
        IdleRollShowClock = settings.ControlPanel.IdleRollShowClock;
        IdleRollShowWeather = settings.ControlPanel.IdleRollShowWeather;
        IdleRollClockFormat = settings.ControlPanel.IdleRollClockFormat;
        IdleRollHoldSeconds = settings.ControlPanel.IdleRollHoldSeconds;

        // Hotkey display
        HotkeyDictate = settings.Hotkeys.Dictate;
        HotkeyRefine = settings.Hotkeys.Refine;
        HotkeyAsk = settings.Hotkeys.Ask;
        HotkeyNote = settings.Hotkeys.Note;
        HotkeyTranslate = settings.Hotkeys.Translate;

        // Wallet balance HUD (visible when AuthMode is Wallet)
        ShowWalletBalance = settings.AuthMode == AuthMode.Wallet;
        if (ShowWalletBalance)
        {
            long credits = settings.Account.WalletBalanceMicro / 1_000;
            WalletBalanceFormatted = credits >= 1_000
                ? (credits / 1_000m).ToString("0.#", CultureInfo.InvariantCulture) + "k C"
                : credits.ToString("N0", CultureInfo.InvariantCulture) + " C";
        }

        // Sync active mode ID from settings
        if (!string.Equals(settings.ActiveDictationModeId, ActiveDictationModeId, StringComparison.Ordinal))
        {
            ActiveDictationModeId = settings.ActiveDictationModeId;
        }

        // Always reload modes — catches creates, deletes, renames, and reorders
        LoadAvailableModes();
    }

    /// <summary>
    /// Loads all available dictation modes from the CRUD system and marks the active one.
    /// Called after mode changes to refresh the UI.
    /// </summary>
    private void LoadAvailableModes()
    {
        var modes = _dictationModes.GetAllModes();
        AvailableModes.Clear();

        foreach (var mode in modes)
        {
            var profile = GetActiveProfile(mode);
            AvailableModes.Add(CreateModeItem(mode, profile));
        }

        // Default to first mode if none selected
        if (string.IsNullOrEmpty(ActiveDictationModeId) && modes.Count > 0)
        {
            ActiveDictationModeId = modes[0].Id;
        }

        Log.Debug("ControlPanel: Refreshed {Count} dictation presets, active={ActiveId}",
            modes.Count, ActiveDictationModeId);
    }

    private DictationProfile GetActiveProfile(DictationMode mode)
    {
        return LlmMode == LlmMode.Local ? mode.LocalProfile : mode.CloudProfile;
    }

    private DictationModeItem CreateModeItem(DictationMode mode, DictationProfile profile)
    {
        bool isActive = string.Equals(mode.Id, ActiveDictationModeId, StringComparison.Ordinal);

        // Build a short subtitle from the profile
        string subtitle = profile.UseLlm
            ? (string.IsNullOrEmpty(profile.ModelName) ? "LLM" : TruncateModel(profile.ModelName))
            : _loc.GetString("ControlPanel_Mode_RawStt");

        // Midnight keeps the V1 teal palette for visual continuity.
        // Frost & Ember derive colors from their ThemePalette.
        var themeName = _themeService.CurrentTheme;
        if (string.Equals(themeName, "Midnight", StringComparison.OrdinalIgnoreCase))
        {
            return new DictationModeItem(
                mode.Id, mode.Title, subtitle, isActive,
                BackgroundHex: isActive ? MidnightActiveBg : MidnightInactiveBg,
                ForegroundHex: isActive ? MidnightActiveFg : MidnightInactiveFg,
                HoverBgHex: isActive ? MidnightActiveHoverBg : MidnightInactiveHoverBg,
                HoverFgHex: MidnightActiveFg,
                PressedBgHex: isActive ? MidnightActivePressedBg : MidnightInactivePressedBg,
                HoverBorderHex: isActive ? MidnightActiveHoverBorder : MidnightInactiveHoverBorder,
                PressedBorderHex: isActive ? MidnightActivePressedBorder : MidnightInactivePressedBorder);
        }

        var palette = ThemeService.GetPalette(themeName);

        // Both active and inactive hover show the accent color (previews "will become selected").
        // Hover text: dark on light themes (readable on accent bg), dark bg color on dark themes.
        string hoverBg = ToHex(palette.Accent);
        string hoverFg = palette.IsDark ? ToHex(palette.Background) : ToHex(palette.Text);
        string pressedBg = ToHex(ShiftColor(palette.Accent, -10));
        string hoverBorder = ToHex(ShiftColor(palette.Accent, 30));
        string pressedBorder = ToHex(ShiftColor(palette.Accent, -15));

        // Active vs inactive differ only in resting background and foreground.
        string bgHex = isActive ? ToHex(palette.Accent) : ToHex(palette.Surface2);
        string fgHex = isActive
            ? (palette.IsDark ? ToHex(palette.Background) : "#FFFFFFFF")
            : ToHex(palette.TextDim);

        return new DictationModeItem(
            mode.Id, mode.Title, subtitle, isActive,
            bgHex, fgHex, hoverBg, hoverFg, pressedBg, hoverBorder, pressedBorder);
    }

    /// <summary>
    /// Truncates a model name to fit in a small button subtitle.
    /// </summary>
    private static string TruncateModel(string modelName)
    {
        // Show last segment if it contains a slash (e.g. "openai/gpt-4o-mini" → "gpt-4o-mini")
        int slashIdx = modelName.LastIndexOf('/');
        string name = slashIdx >= 0 ? modelName[(slashIdx + 1)..] : modelName;

        return name.Length > 14 ? string.Concat(name.AsSpan(0, 12), "..") : name;
    }

    /// <summary>
    /// Formats milliseconds as seconds with consistent digit count.
    /// Under 10s → "0.50s", 10s+ → "12.5s".
    /// </summary>
    private static string FormatMs(long ms)
    {
        double seconds = ms / 1000.0;
        return seconds < 10
            ? seconds.ToString("F2", CultureInfo.InvariantCulture) + "s"
            : seconds.ToString("F1", CultureInfo.InvariantCulture) + "s";
    }
}
