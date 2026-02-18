
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
/// ViewModel for the Control Panel (HUD dashboard).
/// Displays real-time pipeline state, session metrics, quick action toggles, and provider badges.
/// </summary>
public sealed partial class ControlPanelViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly AudioRecorder _recorder;
    private readonly MetricsCollector _metrics;
    private readonly DispatcherQueue _dispatcher;

    // ── Pipeline state ──────────────────────────────────────────────────────

    [ObservableProperty]
    private PipelineState _currentState = PipelineState.Idle;

    [ObservableProperty]
    private string _statusText = "READY";

    [ObservableProperty]
    private string _activeMode = "Standard";

    // ── Session stats ───────────────────────────────────────────────────────

    [ObservableProperty]
    private int _sessionCount;

    [ObservableProperty]
    private int _wordCount;

    [ObservableProperty]
    private double _wordsPerMinute;

    [ObservableProperty]
    private int _tokenCount;

    // ── Performance stats (last pipeline run) ───────────────────────────────

    [ObservableProperty]
    private long _lastRecordingMs;

    [ObservableProperty]
    private long _lastTranscriptionMs;

    [ObservableProperty]
    private long _lastProcessingMs;

    [ObservableProperty]
    private long _lastInjectionMs;

    // ── Provider badges ─────────────────────────────────────────────────────

    [ObservableProperty]
    private string _sttProviderName = "Cloud STT";

    [ObservableProperty]
    private string _llmProviderName = "Gemini";

    // ── Quick action toggles ────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isSoundEnabled = true;

    [ObservableProperty]
    private bool _isCloudMode = true;

    [ObservableProperty]
    private bool _isAdditionalKeyEnabled;

    [ObservableProperty]
    private string _refineMode = "Voice";

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
        AudioRecorder recorder,
        MetricsCollector metrics)
    {
        _settings = settings;
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
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private void SetMode(string mode)
    {
        ActiveMode = mode;
        Log.Debug("ControlPanel: mode set to {Mode}", mode);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        App.Current.ShowSettings();
    }

    // ── Toggle handlers ─────────────────────────────────────────────────────

    partial void OnIsSoundEnabledChanged(bool value)
    {
        var updated = _settings.Current with
        {
            General = _settings.Current.General with { SoundFeedback = value }
        };
        _ = _settings.UpdateAsync(updated);
    }

    partial void OnIsAdditionalKeyEnabledChanged(bool value)
    {
        // Toggle between "Enter" and "" (none)
        string key = value ? "Enter" : "";
        var updated = _settings.Current with
        {
            General = _settings.Current.General with { AdditionalKey = key }
        };
        _ = _settings.UpdateAsync(updated);
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
        _dispatcher.TryEnqueue(() =>
        {
            // Update performance stats
            LastTranscriptionMs = result.TranscriptionMs;
            LastProcessingMs = result.ProcessingMs;
            LastInjectionMs = result.InjectionMs;

            // Update provider badges
            if (result.SttProvider is not null)
            {
                SttProviderName = result.SttProvider;
            }

            if (result.LlmProvider is not null)
            {
                LlmProviderName = result.LlmProvider;
            }

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
        ShowModesRow = settings.ControlPanel.ShowModesRow;
        ShowActionsRow = settings.ControlPanel.ShowActionsRow;
        ShowSessionStats = settings.ControlPanel.ShowSessionStats;
        ShowPerformanceStats = settings.ControlPanel.ShowPerformanceStats;
    }
}
