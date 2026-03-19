
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
    string ForegroundHex);

/// <summary>
/// Refine mode selection (Auto = no audio, Voice = instruction audio).
/// </summary>
public enum RefineMode
{
    Auto,    // Refine Auto (text selection only, no audio)
    Voice,   // Refine Instruction (records audio for spoken command)
}

/// <summary>
/// ViewModel for the Control Panel (HUD dashboard).
/// Displays real-time pipeline state, session metrics, quick action toggles, and provider badges.
/// </summary>
public sealed partial class ControlPanelViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly DictationModeManager _dictationModes;
    private readonly AudioRecorder _recorder;
    private readonly MetricsCollector _metrics;
    private readonly LocalizationService _loc;
    private readonly DispatcherQueue _dispatcher;

    // Badge indicator brushes (local/cloud/off)
    private static readonly SolidColorBrush BadgeLocalBrush = new(ColorHelper.FromArgb(255, 122, 255, 158)); // #7AFF9E green
    private static readonly SolidColorBrush BadgeCloudBrush = new(ColorHelper.FromArgb(255, 78, 168, 222));  // #4EA8DE blue
    private static readonly SolidColorBrush BadgeOffBrush = new(ColorHelper.FromArgb(255, 255, 68, 68));   // #FF4444 red

    // Active-state colors (V1 palette)
    private const string ActiveBgHex = "#00607a";   // --dark-teal-3
    private const string InactiveBgHex = "#00303d";  // --jet-black
    private const string ActiveFgHex = "#ffffff";
    private const string InactiveFgHex = "#888888";

    // ── Pipeline state ──────────────────────────────────────────────────────

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
    private SolidColorBrush _sttBadgeBrush = BadgeCloudBrush;

    [ObservableProperty]
    private SolidColorBrush _llmBadgeBrush = BadgeCloudBrush;

    [ObservableProperty]
    private SolidColorBrush _ttsBadgeBrush = BadgeOffBrush;

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
    public string TtsStateLabel => TtsMode switch
    {
        TtsMode.Local => _loc.GetString("ControlPanel_State_Local"),
        TtsMode.Cloud => _loc.GetString("ControlPanel_State_Cloud"),
        TtsMode.Off => _loc.GetString("ControlPanel_State_Off"),
        _ => _loc.GetString("ControlPanel_State_Off"),
    };

    // ── Toggle state color brushes (for cycle buttons) ──────────────────

    public SolidColorBrush SttStateBrush => IsLocalStt ? BadgeLocalBrush : BadgeCloudBrush;
    public SolidColorBrush LlmStateBrush => LlmMode switch
    {
        LlmMode.Local => BadgeLocalBrush,
        LlmMode.Cloud => BadgeCloudBrush,
        LlmMode.Off => BadgeOffBrush,
        _ => BadgeCloudBrush,
    };
    public SolidColorBrush TtsStateBrush => TtsMode switch
    {
        TtsMode.Local => BadgeLocalBrush,
        TtsMode.Cloud => BadgeCloudBrush,
        TtsMode.Off => BadgeOffBrush,
        _ => BadgeOffBrush,
    };
    public SolidColorBrush SoundStateBrush => IsSoundEnabled ? BadgeLocalBrush : BadgeOffBrush;
    public SolidColorBrush KeyStateBrush => string.IsNullOrEmpty(AdditionalKeyValue) ? BadgeOffBrush : BadgeLocalBrush;
    public SolidColorBrush RefineStateBrush => IsRefineVoice ? BadgeLocalBrush : BadgeCloudBrush;

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

    /// <summary>Chevron icon reflecting expand direction. Points toward the direction content will collapse.</summary>
    public string ExpandCollapseIcon => ExpandUpward
        ? (IsExpanded ? "\uE70D" : "\uE70E")   // Up mode: down-chevron to collapse, up-chevron to expand
        : (IsExpanded ? "\uE70E" : "\uE70D");   // Down mode: up-chevron to collapse, down-chevron to expand

    public ControlPanelViewModel(
        SettingsManager settings,
        DictationModeManager dictationModes,
        AudioRecorder recorder,
        MetricsCollector metrics,
        LocalizationService loc)
    {
        _settings = settings;
        _dictationModes = dictationModes;
        _recorder = recorder;
        _metrics = metrics;
        _loc = loc;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _statusText = _loc.GetString("ControlPanel_State_Ready");

        // Subscribe to recorder events
        _recorder.RecordingStarted += OnRecordingStarted;
        _recorder.RecordingStopped += OnRecordingStopped;

        // Subscribe to settings changes
        _settings.SettingsChanged += OnSettingsChanged;

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

    partial void OnLlmModeChanged(LlmMode value)
    {
        if (!_suppressSave)
        {
            bool rawMode = value == LlmMode.Off;
            string llmProvider = value switch
            {
                LlmMode.Local => "ollama",
                LlmMode.Cloud => "gemini",
                _ => _settings.Current.ModeProfiles.GetValueOrDefault("dictate_0", new ModeSettings()).LlmProvider,
            };
            string profileName = value == LlmMode.Local ? "Local" : "Cloud";

            var profiles = new Dictionary<string, ModeSettings>(_settings.Current.ModeProfiles);
            if (!rawMode)
            {
                string[] modes = ["dictate", "refine", "ask", "translate", "note", "chat"];
                foreach (var mode in modes)
                {
                    for (int p = 0; p < 2; p++)
                    {
                        string key = $"{mode}_{p}";
                        var existing = profiles.TryGetValue(key, out var ms) ? ms : new ModeSettings();
                        profiles[key] = existing with { LlmProvider = llmProvider, UseLlm = true };
                    }
                }
            }

            var updated = _settings.Current with
            {
                General = _settings.Current.General with { RawModeOverride = rawMode },
                ModeProfiles = profiles,
                ActiveProfileName = value == LlmMode.Local ? "Local" : "Cloud",
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
        SttBadgeBrush = IsLocalStt ? BadgeLocalBrush : BadgeCloudBrush;

        // LLM: red = off (RAW mode), green = local (Ollama), blue = cloud
        switch (LlmMode)
        {
            case LlmMode.Off:
                LlmBadgeBrush = BadgeOffBrush;
                LlmProviderName = "Disabled (RAW)";
                break;
            case LlmMode.Local:
                LlmBadgeBrush = BadgeLocalBrush;
                break;
            case LlmMode.Cloud:
                LlmBadgeBrush = BadgeCloudBrush;
                break;
        }

        // TTS: red = off, green = local (Kokoro), blue = cloud
        switch (TtsMode)
        {
            case TtsMode.Off:
                TtsBadgeBrush = BadgeOffBrush;
                TtsProviderName = "Disabled";
                break;
            case TtsMode.Local:
                TtsBadgeBrush = BadgeLocalBrush;
                TtsProviderName = "Kokoro (local)";
                break;
            case TtsMode.Cloud:
                TtsBadgeBrush = BadgeCloudBrush;
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
                    BarPosition = value
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
        // Read STT/LLM provider from ModeProfiles (dictate_0 is the reference slot)
        var refSlot = settings.ModeProfiles.GetValueOrDefault("dictate_0", new ModeSettings());
        IsLocalStt = string.Equals(refSlot.SttProvider, "whisper", StringComparison.OrdinalIgnoreCase);

        // Derive LlmMode from RawModeOverride + LlmProvider
        if (settings.General.RawModeOverride)
        {
            LlmMode = LlmMode.Off;
        }
        else
        {
            LlmMode = string.Equals(refSlot.LlmProvider, "ollama", StringComparison.OrdinalIgnoreCase)
                ? LlmMode.Local
                : LlmMode.Cloud;
        }

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
        BarPosition = settings.ControlPanel.BarPosition ?? "TopRight";
        AutoCollapseEnabled = settings.ControlPanel.AutoCollapseEnabled;
        AutoCollapseDelaySeconds = settings.ControlPanel.AutoCollapseDelaySeconds;
        WaveformStyle = settings.ControlPanel.WaveformStyle;
        AutoHideEnabled = settings.ControlPanel.AutoHideEnabled;
        AutoHideDelaySeconds = settings.ControlPanel.AutoHideDelaySeconds;

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
            decimal balanceDollars = settings.Account.WalletBalanceMicro / 1_000_000m;
            WalletBalanceFormatted = balanceDollars.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
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

        return new DictationModeItem(
            mode.Id,
            mode.Title,
            subtitle,
            isActive,
            BackgroundHex: isActive ? ActiveBgHex : InactiveBgHex,
            ForegroundHex: isActive ? ActiveFgHex : InactiveFgHex);
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
