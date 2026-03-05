
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Pipeline;
using Microsoft.UI.Dispatching;
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
    private readonly DispatcherQueue _dispatcher;

    // Active-state colors (V1 palette)
    private const string ActiveBgHex = "#00607a";   // --dark-teal-3
    private const string InactiveBgHex = "#00303d";  // --jet-black
    private const string ActiveFgHex = "#ffffff";
    private const string InactiveFgHex = "#888888";

    // ── Pipeline state ──────────────────────────────────────────────────────

    [ObservableProperty]
    private PipelineState _currentState = PipelineState.Idle;

    [ObservableProperty]
    private string _statusText = "READY";

    // ── Dictation modes (CRUD system) ───────────────────────────────────────

    /// <summary>Dynamic list of available dictation presets (user-created).</summary>
    [ObservableProperty]
    private ObservableCollection<DictationModeItem> _availableModes = [];

    /// <summary>ID of the currently active dictation mode (persisted in settings).</summary>
    [ObservableProperty]
    private string? _activeDictationModeId;

    /// <summary>RAW mode override toggle (independent from normal mode selection).</summary>
    [ObservableProperty]
    private bool _isRawModeEnabled;

    /// <summary>Display-only: Title of the currently active mode (for XAML compatibility).</summary>
    public string ActiveMode => _dictationModes.GetAllModes()
        .FirstOrDefault(m => string.Equals(m.Id, ActiveDictationModeId, StringComparison.Ordinal))?.Title ?? "Standard";

    // ── Session stats ───────────────────────────────────────────────────────

    [ObservableProperty]
    private int _sessionCount;

    [ObservableProperty]
    private int _wordCount;

    [ObservableProperty]
    private double _wordsPerMinute;

    [ObservableProperty]
    private int _tokenCount;

    /// <summary>WPM display string ("--" when 0, otherwise rounded value).</summary>
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

    // ── Provider badges ─────────────────────────────────────────────────────

    [ObservableProperty]
    private string _sttProviderName = "--";

    [ObservableProperty]
    private string _llmProviderName = "--";

    [ObservableProperty]
    private string _authBadgeText = "API";

    // ── Quick action toggles ────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isSoundEnabled = true;

    [ObservableProperty]
    private bool _isLocalMode;

    [ObservableProperty]
    private bool _isAdditionalKeyEnabled;

    [ObservableProperty]
    private RefineMode _refineMode = RefineMode.Voice;

    /// <summary>Whether the refine toggle is on (Voice mode) or off (Auto mode).</summary>
    [ObservableProperty]
    private bool _isRefineVoice = true;

    // ── Toggle labels ─────────────────────────────────────────────────────

    public string SoundLabel => IsSoundEnabled ? "SOUND: ON" : "SOUND: OFF";
    public string LocalLabel => IsLocalMode ? "LOCAL" : "CLOUD";
    public string KeyLabel => IsAdditionalKeyEnabled ? "+KEY: ON" : "+KEY: OFF";
    public string RawLabel => IsRawModeEnabled ? "RAW: ON" : "RAW: OFF";
    public string RefineLabel => RefineMode == RefineMode.Voice ? "REFINE: VOICE" : "REFINE: AUTO";

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

    public ControlPanelViewModel(
        SettingsManager settings,
        DictationModeManager dictationModes,
        AudioRecorder recorder,
        MetricsCollector metrics)
    {
        _settings = settings;
        _dictationModes = dictationModes;
        _recorder = recorder;
        _metrics = metrics;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Subscribe to recorder events
        _recorder.RecordingStarted += OnRecordingStarted;
        _recorder.RecordingStopped += OnRecordingStopped;

        // Subscribe to settings changes
        _settings.SettingsChanged += OnSettingsChanged;

        // Load initial state from settings
        LoadFromSettings(_settings.Current);

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
    private void ToggleRawMode()
    {
        IsRawModeEnabled = !IsRawModeEnabled;
        Log.Information("ControlPanel: RAW mode toggled to {IsEnabled}", IsRawModeEnabled);
    }

    [RelayCommand]
    private void ToggleRefineMode()
    {
        RefineMode = RefineMode == RefineMode.Auto ? RefineMode.Voice : RefineMode.Auto;
        IsRefineVoice = RefineMode == RefineMode.Voice;
        Log.Information("ControlPanel: Refine mode toggled to {Mode}", RefineMode);
    }

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
        var updated = _settings.Current with
        {
            General = _settings.Current.General with { SoundFeedback = value }
        };
        _ = _settings.UpdateAsync(updated);
        OnPropertyChanged(nameof(SoundLabel));
    }

    partial void OnIsLocalModeChanged(bool value)
    {
        string profileName = value ? "Local" : "Cloud";
        var updated = _settings.Current with
        {
            ActiveProfileName = profileName
        };
        _ = _settings.UpdateAsync(updated);

        AuthBadgeText = value ? "LOC" : "API";
        OnPropertyChanged(nameof(LocalLabel));

        // Refresh mode items (subtitle may change between Cloud/Local profile)
        LoadAvailableModes();

        Log.Information("ControlPanel: Profile switched to {Profile}", profileName);
    }

    partial void OnIsAdditionalKeyEnabledChanged(bool value)
    {
        // Preserve the existing key choice — only toggle enable/disable
        string currentKey = _settings.Current.General.AdditionalKey;
        string key = value
            ? (string.IsNullOrEmpty(currentKey) ? "Enter" : currentKey)
            : "";

        var updated = _settings.Current with
        {
            General = _settings.Current.General with { AdditionalKey = key }
        };
        _ = _settings.UpdateAsync(updated);
        OnPropertyChanged(nameof(KeyLabel));
    }

    partial void OnIsRawModeEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(RawLabel));
        Log.Information("ControlPanel: RAW mode set to {IsEnabled}", value);
    }

    partial void OnIsRefineVoiceChanged(bool value)
    {
        RefineMode = value ? RefineMode.Voice : RefineMode.Auto;
        OnPropertyChanged(nameof(RefineLabel));
    }

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
                PipelineState.Idle => "READY",
                PipelineState.Recording => "LISTENING",
                PipelineState.Transcribing => "TRANSCRIBING",
                PipelineState.Processing => "THINKING",
                PipelineState.Injecting => "TYPING",
                PipelineState.Error => "ERROR",
                _ => "READY",
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
            StatusText = "READY";

            // Refresh session stats
            RefreshSessionStats();
        });
    }

    /// <summary>
    /// Refreshes session stats from the metrics collector.
    /// </summary>
    public void RefreshSessionStats()
    {
        var stats = _metrics.GetSessionStats();
        SessionCount = stats.Sessions;
        WordCount = stats.Words;
        WordsPerMinute = Math.Round(stats.AverageLatencyMs > 0
            ? stats.Words / (stats.AverageLatencyMs / 60000.0)
            : 0, 1);
        TokenCount = (int)(stats.Words * 1.3); // Rough estimate: ~1.3 tokens per word
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void OnRecordingStarted(object? sender, RecordingStartedEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            CurrentState = PipelineState.Recording;
            StatusText = "LISTENING";
        });
    }

    private void OnRecordingStopped(object? sender, RecordingStoppedEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (CurrentState == PipelineState.Recording)
            {
                CurrentState = PipelineState.Idle;
                StatusText = "READY";
            }
        });
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        _dispatcher.TryEnqueue(() => LoadFromSettings(settings));
    }

    private void LoadFromSettings(AppSettings settings)
    {
        IsSoundEnabled = settings.General.SoundFeedback;
        IsAdditionalKeyEnabled = !string.IsNullOrEmpty(settings.General.AdditionalKey);
        IsLocalMode = string.Equals(settings.ActiveProfileName, "Local", StringComparison.OrdinalIgnoreCase);
        AuthBadgeText = IsLocalMode ? "LOC" : "API";

        ShowModesRow = settings.ControlPanel.ShowModesRow;
        ShowActionsRow = settings.ControlPanel.ShowActionsRow;
        ShowSessionStats = settings.ControlPanel.ShowSessionStats;
        ShowPerformanceStats = settings.ControlPanel.ShowPerformanceStats;

        // Hotkey display
        HotkeyDictate = settings.Hotkeys.Dictate;
        HotkeyRefine = settings.Hotkeys.Refine;
        HotkeyAsk = settings.Hotkeys.Ask;
        HotkeyNote = settings.Hotkeys.Note;
        HotkeyTranslate = settings.Hotkeys.Translate;

        // Sync active mode ID from settings
        if (!string.Equals(settings.ActiveDictationModeId, ActiveDictationModeId, StringComparison.Ordinal))
        {
            ActiveDictationModeId = settings.ActiveDictationModeId;
            LoadAvailableModes(); // Refresh mode highlighting
        }
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
        return IsLocalMode ? mode.LocalProfile : mode.CloudProfile;
    }

    private DictationModeItem CreateModeItem(DictationMode mode, DictationProfile profile)
    {
        bool isActive = string.Equals(mode.Id, ActiveDictationModeId, StringComparison.Ordinal);

        // Build a short subtitle from the profile
        string subtitle = profile.UseLlm
            ? (string.IsNullOrEmpty(profile.ModelName) ? "LLM" : TruncateModel(profile.ModelName))
            : "Raw STT";

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
