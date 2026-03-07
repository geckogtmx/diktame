
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.App.Services;
using DiktaMe.Core.Account;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Input;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.SystemManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Serilog;

namespace DiktaMe.App.ViewModels;
public sealed partial class LoadingViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly HistoryManager _history;
    private readonly SnippetManager _snippets;
    private readonly OllamaManager _ollama;
    private readonly HotkeyManager _hotkeyManager;
    private readonly PipelineFactory _pipelineFactory;
    private readonly NotificationService _notifications;
    private readonly AudioDucker _audioDucker;
    private readonly DictationModeManager _dictationModes;
    private readonly PipelineConfigManager _pipelines;
    private readonly TextInjector _textInjector;
    private readonly ControlPanelViewModel _controlPanel;
    private readonly IAccountService _accountService;
    private readonly ITrialService _trialService;
    private readonly LocalizationService _loc;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private double _progress;

    private AudioRecorder? _currentRecorder;
    private CancellationTokenSource? _recordingCts;
    private DispatcherQueue? _uiDispatcher;
    private bool _isRecording;

    public event Action? LoadingComplete;

    public LoadingViewModel(
        SettingsManager settings,
        HistoryManager history,
        SnippetManager snippets,
        OllamaManager ollama,
        HotkeyManager hotkeyManager,
        PipelineFactory pipelineFactory,
        NotificationService notifications,
        AudioDucker audioDucker,
        DictationModeManager dictationModes,
        PipelineConfigManager pipelines,
        TextInjector textInjector,
        ControlPanelViewModel controlPanel,
        IAccountService accountService,
        ITrialService trialService,
        LocalizationService loc)
    {
        _settings = settings;
        _history = history;
        _snippets = snippets;
        _ollama = ollama;
        _hotkeyManager = hotkeyManager;
        _pipelineFactory = pipelineFactory;
        _notifications = notifications;
        _audioDucker = audioDucker;
        _dictationModes = dictationModes;
        _pipelines = pipelines;
        _textInjector = textInjector;
        _controlPanel = controlPanel;
        _accountService = accountService;
        _trialService = trialService;
        _loc = loc;
        _statusText = _loc.GetString("Loading_Initializing");
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Step 1: Load settings
            StatusText = _loc.GetString("Loading_Settings");
            Progress = 0;
            await _settings.LoadAsync();
            Progress = 25;

            // Step 2: Initialize database
            StatusText = _loc.GetString("Loading_Database");
            await _history.InitAsync();
            Progress = 50;

            // Step 3: Load snippets
            StatusText = _loc.GetString("Loading_Snippets");
            await _snippets.LoadAsync();
            Progress = 75;

            // Step 4: Check Ollama (if configured as local LLM)
            StatusText = _loc.GetString("Loading_LocalServices");
            try
            {
                await _ollama.CheckAsync(_settings.Current.OllamaModel);
            }
            catch (Exception ex)
            {
                // Non-fatal — Ollama may not be installed
                Log.Debug(ex, "Ollama check skipped during loading");
            }
            Progress = 85;

            // Step 5: Sync trial account status (if signed in)
            if (_accountService.HasValidToken)
            {
                StatusText = _loc.GetString("Loading_Account");
                try
                {
                    await _trialService.RefreshStatusAsync();
                }
                catch (Exception ex)
                {
                    // Non-fatal — network may be unavailable
                    Log.Debug(ex, "Trial status refresh skipped during loading");
                }
            }
            Progress = 90;

            // Step 6: Start hotkey manager and register hotkeys
            StatusText = _loc.GetString("Loading_Hotkeys");
            InitializeHotkeys();
            Progress = 100;

            StatusText = _loc.GetString("Loading_Ready");
        }
        catch (Exception ex)
        {
            StatusText = _loc.GetString("Loading_Error");
            Log.Error(ex, "Loading initialization failed");
            await Task.Delay(1500); // Let user see the error briefly
        }

        LoadingComplete?.Invoke();
    }

    private void InitializeHotkeys()
    {
        try
        {
            Log.Information("Starting hotkey initialization...");

            // Capture UI dispatcher while we're on the UI thread —
            // OnHotkeyPressed fires on the message-pump thread where
            // DispatcherQueue.GetForCurrentThread() returns null.
            _uiDispatcher = DispatcherQueue.GetForCurrentThread();

            // Start the background message pump
            _hotkeyManager.Start();
            Log.Information("HotkeyManager.Start() completed");

            // Subscribe to events
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
            _hotkeyManager.RegistrationFailed += OnHotkeyRegistrationFailed;
            Log.Information("Event handlers subscribed");

            // Register all configured hotkeys
            var hotkeys = _settings.Current.Hotkeys;
            Log.Information("Registering hotkeys: Dictate={Dictate}, Chat={Chat}, etc.",
                hotkeys.Dictate, hotkeys.Chat);

            RegisterAllHotkeys(hotkeys);

            // Re-register when settings change
            _settings.SettingsChanged += (_, newSettings) =>
            {
                Log.Information("Settings changed, re-registering hotkeys");
                RegisterAllHotkeys(newSettings.Hotkeys);
            };

            Log.Information("HotkeyManager initialized with {Count} hotkeys", 7);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize hotkeys");
        }
    }

    private void RegisterAllHotkeys(HotkeySettings hotkeys)
    {
        bool success;
        success = _hotkeyManager.Register(HotkeyId.Dictate, hotkeys.Dictate);
        Log.Debug("Register Dictate ({Hotkey}): {Success}", hotkeys.Dictate, success);

        success = _hotkeyManager.Register(HotkeyId.Refine, hotkeys.Refine);
        Log.Debug("Register Refine ({Hotkey}): {Success}", hotkeys.Refine, success);

        success = _hotkeyManager.Register(HotkeyId.Ask, hotkeys.Ask);
        Log.Debug("Register Ask ({Hotkey}): {Success}", hotkeys.Ask, success);

        success = _hotkeyManager.Register(HotkeyId.Translate, hotkeys.Translate);
        Log.Debug("Register Translate ({Hotkey}): {Success}", hotkeys.Translate, success);

        success = _hotkeyManager.Register(HotkeyId.Oops, hotkeys.Oops);
        Log.Debug("Register Oops ({Hotkey}): {Success}", hotkeys.Oops, success);

        success = _hotkeyManager.Register(HotkeyId.Note, hotkeys.Note);
        Log.Debug("Register Note ({Hotkey}): {Success}", hotkeys.Note, success);

        success = _hotkeyManager.Register(HotkeyId.Chat, hotkeys.Chat);
        Log.Debug("Register Chat ({Hotkey}): {Success}", hotkeys.Chat, success);
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        Log.Information("Hotkey pressed: {Id}", e.Id);

        // Dispatch to UI thread — use the dispatcher captured during init
        // (this handler fires on the message-pump thread, not the UI thread)
        var dispatcherQueue = _uiDispatcher;

        if (dispatcherQueue is null)
        {
            Log.Error("Hotkey {Id}: UI dispatcher not available — cannot dispatch", e.Id);
            return;
        }

        dispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                // Toggle-stop: if already recording, stop instead of starting a new pipeline
                if (_isRecording && _currentRecorder is not null)
                {
                    Log.Information("Hotkey {Id}: stopping active recording", e.Id);
                    _ = _currentRecorder.StopRecordingAsync();
                    return;
                }

                switch (e.Id)
                {
                    case HotkeyId.Chat:
                        // Open Quick Chat window
                        App.Current.ToggleQuickChat();
                        break;

                    case HotkeyId.Dictate:
                        _ = RunDictationPipelineAsync();
                        break;

                    case HotkeyId.Refine:
                        _ = RunRefinePipelineAsync();
                        break;

                    case HotkeyId.Ask:
                        _ = RunAskPipelineAsync();
                        break;

                    case HotkeyId.Translate:
                        _ = RunTranslatePipelineAsync();
                        break;

                    case HotkeyId.Note:
                        _ = RunNotePipelineAsync();
                        break;

                    case HotkeyId.Oops:
                        Log.Warning("Hotkey Oops triggered but Undo functionality not yet implemented");
#pragma warning disable MA0026 // Deferred: Undo functionality requires clipboard history tracking
                        // TODO: Implement Undo/Oops functionality
#pragma warning restore MA0026
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling hotkey {Id}", e.Id);
            }
        });
    }

    private void OnHotkeyRegistrationFailed(object? sender, HotkeyRegistrationFailedEventArgs e)
    {
        Log.Warning("Hotkey registration failed: {Id} = '{HotkeyString}' - {Reason}",
            e.Id, e.HotkeyString, e.Reason);

        _notifications.ShowToast(
            _loc.GetString("Loading_HotkeyConflict_Title"),
            _loc.GetFormatted("Loading_HotkeyConflict_Message", e.Id, e.HotkeyString),
            NotificationType.Error);
    }

    // ── Helper Methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Records audio from the configured input device using event-based async,
    /// with audio ducking and user notifications.
    /// </summary>
    /// <param name="mode">Display name for logging/toast (e.g. "Dictate", "Ask").</param>
    /// <param name="isDictate">True for dictation modes (uses start/stop sounds), false for utility (uses utility sound).</param>
    /// <returns>A tuple containing the audio file path and recording duration in milliseconds.</returns>
    private Task<(string? FilePath, long DurationMs)> RecordAudioAsync(string mode, bool isDictate)
    {
        var tcs = new TaskCompletionSource<(string?, long)>();

        // Get fresh recorder instance
        _currentRecorder?.Dispose();
        _currentRecorder = App.Current.Services.GetRequiredService<AudioRecorder>();

        // Capture sound settings for the stop handler (which fires on a background thread)
        var soundSettings = _settings.Current.Sound ?? new();
        string stopSound = isDictate ? soundSettings.StopSound : soundSettings.UtilitySound;

        // Subscribe to both stop events (auto-stop on duration limit, manual stop on toggle)
        EventHandler<RecordingStoppedEventArgs>? stopHandler = null;
        stopHandler = (_, args) =>
        {
            _currentRecorder!.AutoStopped -= stopHandler;
            _currentRecorder!.RecordingStopped -= stopHandler;
            _isRecording = false;
            _notifications.PlayCustomSound(stopSound);
            tcs.TrySetResult((args.FilePath, args.DurationMs));
        };
        _currentRecorder.AutoStopped += stopHandler;
        _currentRecorder.RecordingStopped += stopHandler;

        // Get audio settings
        var audio = _settings.Current.Audio;
        string? deviceLabel = string.IsNullOrEmpty(audio.DeviceName) ? null : audio.DeviceName;
        int maxDuration = audio.MaxDurationSeconds;

        // Start audio ducking if enabled
        if (_settings.Current.AudioDucking.Enabled)
        {
            _audioDucker.IsEnabled = true;
            _audioDucker.DuckLevel = _settings.Current.AudioDucking.DuckLevelPercent / 100f;
            _audioDucker.Duck();
        }

        // Start recording (all params are optional)
        _isRecording = true;
        _currentRecorder.StartRecording(
            deviceLabel: deviceLabel,
            deviceId: null, // let AudioDeviceManager resolve from label
            maxDurationSeconds: maxDuration);

        Log.Information("{Mode}: Recording started (max {MaxSec}s)", mode, maxDuration);

        // Play start sound (after recording has begun)
        string startSound = isDictate ? soundSettings.StartSound : soundSettings.UtilitySound;
        _notifications.PlayCustomSound(startSound);

        return tcs.Task;
    }

    // ── Pipeline Handlers ─────────────────────────────────────────────────────

    private async Task RunDictationPipelineAsync()
    {
        if (_settings.Current.General.StreamingEnabled && _pipelineFactory.CanStreamDictation())
        {
            await RunStreamingDictationAsync();
        }
        else
        {
            await RunBatchDictationAsync();
        }
    }

    private async Task RunStreamingDictationAsync()
    {
        StreamingDictationPipeline? streamingPipeline = null;
        try
        {
            Log.Information("Starting Streaming Dictate pipeline...");

            streamingPipeline = _pipelineFactory.CreateStreamingDictationPipeline();
            if (streamingPipeline is null)
            {
                Log.Warning("Streaming pipeline unavailable, falling back to batch");
                await RunBatchDictationAsync();
                return;
            }

            // Wire state/completed events to ControlPanel
            streamingPipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            streamingPipeline.Completed += _controlPanel.OnPipelineCompleted;

            // Get fresh recorder
            _currentRecorder?.Dispose();
            _currentRecorder = App.Current.Services.GetRequiredService<AudioRecorder>();

            // Build DictationOptions (streaming is always raw mode)
            var options = new DictationOptions
            {
                RawMode = true,
                Language = _settings.Current.General.Language,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = _settings.Current.General.TrailingSpace,
                },
            };

            // Start audio ducking
            if (_settings.Current.AudioDucking.Enabled)
            {
                _audioDucker.IsEnabled = true;
                _audioDucker.DuckLevel = _settings.Current.AudioDucking.DuckLevelPercent / 100f;
                _audioDucker.Duck();
            }

            // Start recording — AudioRecorder fires AudioDataAvailable events
            var audio = _settings.Current.Audio;
            string? deviceLabel = string.IsNullOrEmpty(audio.DeviceName) ? null : audio.DeviceName;
            _isRecording = true;
            _currentRecorder.StartRecording(
                deviceLabel: deviceLabel,
                deviceId: null,
                maxDurationSeconds: audio.MaxDurationSeconds);

            var soundSettings = _settings.Current.Sound ?? new();
            _notifications.PlayCustomSound(soundSettings.StartSound);

            // Run pipeline (blocks until recording stops + finals drained)
            _recordingCts = new CancellationTokenSource();
            var result = await streamingPipeline.RunAsync(
                _currentRecorder, options, _recordingCts.Token);

            _isRecording = false;
            _notifications.PlayCustomSound(soundSettings.StopSound);

            if (result.IsSuccess)
            {
                Log.Information("StreamingDictate: Success, {Chars} chars injected", result.Text.Length);
            }
            else
            {
                Log.Warning("StreamingDictate: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Streaming Dictate pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            _isRecording = false;
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
            if (streamingPipeline is not null)
            {
                await streamingPipeline.DisposeAsync();
            }
        }
    }

    private async Task RunBatchDictationAsync()
    {
        try
        {
            Log.Information("Starting Dictate pipeline...");

            // Record audio (waits for auto-stop event)
            var (audioFile, recordingDurationMs) = await RecordAudioAsync("Dictate", isDictate: true);
            if (audioFile == null)
            {
                Log.Warning("Dictate: No audio file produced");
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error);
                return;
            }

            // Stop ducking
            _audioDucker.Restore();

            Log.Information("Dictate: Recording complete, processing...");

            // Get active mode from settings (instead of always using modes[0])
            var modes = _dictationModes.GetAllModes();
            string? activeModeId = _settings.Current.ActiveDictationModeId;

            // Fallback to first mode if ID is null or invalid
            DictationMode? activeMode = modes.FirstOrDefault(m => string.Equals(m.Id, activeModeId, StringComparison.Ordinal))
                                        ?? modes.FirstOrDefault();

            if (activeMode == null)
            {
                Log.Warning("Dictate: No dictation modes configured");
                _notifications.ShowToast("Error", _loc.GetString("Loading_NoModesConfigured"), NotificationType.Error);
                return;
            }

            DictationProfile profile = _dictationModes.GetActiveProfile(activeMode.Id);
            Log.Information("Dictate: Using mode '{ModeTitle}' (ID: {ModeId})", activeMode.Title, activeMode.Id);

            // Build DictationOptions with all required fields
            var options = new DictationOptions
            {
                SystemPrompt = profile.UseLlm ? profile.SystemPrompt : null,
                RawMode = _controlPanel.IsRawModeEnabled || !profile.UseLlm,
                ModelName = profile.ModelName, // J.5: Per-mode model selection
                Language = _settings.Current.General.Language,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = _settings.Current.General.TrailingSpace,
                    AdditionalKey = string.IsNullOrEmpty(_settings.Current.General.AdditionalKey)
                        ? null
                        : _settings.Current.General.AdditionalKey,
                },
                RecordingDurationMs = recordingDurationMs,
            };

            // Create pipeline and run with correct signature
            var pipeline = _pipelineFactory.CreateDictationPipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            // Notify ControlPanel of pipeline completion (for telemetry)
            _controlPanel.OnPipelineCompleted(this, result);

            // Access correct PipelineResult properties
            if (result.IsSuccess)
            {
                Log.Information("Dictate: Success, {Chars} chars injected", result.Text.Length);
            }
            else
            {
                Log.Warning("Dictate: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Dictate pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task RunRefinePipelineAsync()
    {
        bool isAutoMode = _controlPanel.RefineMode == RefineMode.Auto;

        if (isAutoMode)
        {
            await RunRefineAutoAsync();
        }
        else
        {
            await RunRefineVoiceAsync();
        }
    }

    private async Task RunRefineAutoAsync()
    {
        try
        {
            Log.Information("Starting Refine Auto pipeline (no audio)...");

            var soundSettings = _settings.Current.Sound ?? new();
            _notifications.PlayCustomSound(soundSettings.UtilitySound);

            string pipelineType = "refine_auto";
            UtilityProfile profile = _pipelines.GetActiveProfile(pipelineType);

            var options = new RefineOptions
            {
                SystemPrompt = profile.SystemPrompt ?? PromptDefaults.RefineAuto,
                ModelName = profile.ModelName,
                Language = _settings.Current.General.Language,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = _settings.Current.General.TrailingSpace,
                    AdditionalKey = string.IsNullOrEmpty(_settings.Current.General.AdditionalKey)
                        ? null
                        : _settings.Current.General.AdditionalKey,
                },
            };

            var pipeline = _pipelineFactory.CreateRefineAutoPipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(null, options, _recordingCts.Token);

            _controlPanel.OnPipelineCompleted(this, result);
            _notifications.PlayCustomSound(soundSettings.UtilitySound);

            if (result.IsSuccess)
            {
                Log.Information("Refine Auto: Success, {Chars} chars", result.Text.Length);
            }
            else
            {
                Log.Warning("Refine Auto: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Refine Auto pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task RunRefineVoiceAsync()
    {
        try
        {
            Log.Information("Starting Refine Voice pipeline...");

            // Record audio (waits for auto-stop event)
            var (audioFile, recordingDurationMs) = await RecordAudioAsync("Refine", isDictate: false);
            if (audioFile == null)
            {
                Log.Warning("Refine: No audio file produced");
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error);
                return;
            }

            // Stop ducking
            _audioDucker.Restore();

            Log.Information("Refine Voice: Recording complete, processing...");

            string pipelineType = "refine_instruction";
            UtilityProfile profile = _pipelines.GetActiveProfile(pipelineType);

            var options = new RefineOptions
            {
                SystemPrompt = profile.SystemPrompt ?? PromptDefaults.Refine,
                ModelName = profile.ModelName,
                Language = _settings.Current.General.Language,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = _settings.Current.General.TrailingSpace,
                    AdditionalKey = string.IsNullOrEmpty(_settings.Current.General.AdditionalKey)
                        ? null
                        : _settings.Current.General.AdditionalKey,
                },
                RecordingDurationMs = recordingDurationMs,
            };

            var pipeline = _pipelineFactory.CreateRefinePipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            _controlPanel.OnPipelineCompleted(this, result);

            if (result.IsSuccess)
            {
                Log.Information("Refine Voice: Success, {Chars} chars", result.Text.Length);
            }
            else
            {
                Log.Warning("Refine Voice: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Refine Voice pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task RunAskPipelineAsync()
    {
        try
        {
            Log.Information("Starting Ask pipeline...");

            // Record audio (waits for auto-stop event)
            var (audioFile, recordingDurationMs) = await RecordAudioAsync("Ask", isDictate: false);
            if (audioFile == null)
            {
                Log.Warning("Ask: No audio file produced");
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error);
                return;
            }

            // Stop ducking
            _audioDucker.Restore();

            Log.Information("Ask: Recording complete, processing...");

            // Get active profile from CRUD system
            UtilityProfile profile = _pipelines.GetActiveProfile("ask");

            // Build AskOptions
            var options = new AskOptions
            {
                SystemPrompt = profile.SystemPrompt ?? PromptDefaults.Ask, // fallback to default
                ModelName = profile.ModelName, // J.5: Per-mode model selection
                Language = _settings.Current.General.Language,
                RecordingDurationMs = recordingDurationMs,
            };

            var pipeline = _pipelineFactory.CreateAskPipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            // Notify ControlPanel of pipeline completion (for telemetry)
            _controlPanel.OnPipelineCompleted(this, result);

            if (result.IsSuccess)
            {
                Log.Information("Ask: Answer = {Answer}", result.Text);

                // Route output based on user preference
                AskOutputMode outputMode = _settings.Current.General.AskOutput;
                switch (outputMode)
                {
                    case AskOutputMode.ToastOnly:
                        _notifications.ShowToast("Answer", result.Text, NotificationType.Success);
                        break;

                    case AskOutputMode.ClipboardOnly:
                        ClipboardManager.SetText(result.Text);
                        break;

                    case AskOutputMode.InjectOnly:
                        _textInjector.InjectText(
                            result.Text,
                            _settings.Current.General.TrailingSpace,
                            string.IsNullOrEmpty(_settings.Current.General.AdditionalKey)
                                ? null
                                : _settings.Current.General.AdditionalKey);
                        break;

                    case AskOutputMode.ClipboardAndToast:
                        ClipboardManager.SetText(result.Text);
                        _notifications.ShowToast("Answer (copied)", result.Text, NotificationType.Success);
                        break;

                    default:
                        _notifications.ShowToast("Answer", result.Text, NotificationType.Success);
                        break;
                }
            }
            else
            {
                Log.Warning("Ask: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Ask pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task RunTranslatePipelineAsync()
    {
        try
        {
            Log.Information("Starting Translate pipeline...");

            // Record audio (waits for auto-stop event)
            var (audioFile, recordingDurationMs) = await RecordAudioAsync("Translate", isDictate: false);
            if (audioFile == null)
            {
                Log.Warning("Translate: No audio file produced");
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error);
                return;
            }

            // Stop ducking
            _audioDucker.Restore();

            Log.Information("Translate: Recording complete, processing...");

            // Get active profile from CRUD system
            UtilityProfile profile = _pipelines.GetActiveProfile("translate");

            // Build TranslateOptions with auto language detection
            var options = new TranslateOptions
            {
                SystemPrompt = profile.SystemPrompt ?? PromptDefaults.Translate, // fallback to default
                ModelName = profile.ModelName, // J.5: Per-mode model selection
                Language = "auto", // auto-detect source language (EN/ES)
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = _settings.Current.General.TrailingSpace,
                    AdditionalKey = string.IsNullOrEmpty(_settings.Current.General.AdditionalKey)
                        ? null
                        : _settings.Current.General.AdditionalKey,
                },
                RecordingDurationMs = recordingDurationMs,
            };

            var pipeline = _pipelineFactory.CreateTranslatePipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            // Notify ControlPanel of pipeline completion (for telemetry)
            _controlPanel.OnPipelineCompleted(this, result);

            if (result.IsSuccess)
            {
                Log.Information("Translate: Success, {Chars} chars", result.Text.Length);
            }
            else
            {
                Log.Warning("Translate: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Translate pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task RunNotePipelineAsync()
    {
        try
        {
            Log.Information("Starting Note pipeline...");

            // Record audio (waits for auto-stop event)
            var (audioFile, recordingDurationMs) = await RecordAudioAsync("Note", isDictate: false);
            if (audioFile == null)
            {
                Log.Warning("Note: No audio file produced");
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error);
                return;
            }

            // Stop ducking
            _audioDucker.Restore();

            Log.Information("Note: Recording complete, processing...");

            // Get active profile from CRUD system
            UtilityProfile profile = _pipelines.GetActiveProfile("note");

            // Build NoteOptions with notes file path
            var options = new NoteOptions
            {
                SystemPrompt = profile.SystemPrompt, // from CRUD profile
                ModelName = profile.ModelName, // J.5: Per-mode model selection
                Language = _settings.Current.General.Language,
                NotesFilePath = _settings.Current.NotesFilePath, // required
                TimestampFormat = "yyyy-MM-dd HH:mm:ss",
                RecordingDurationMs = recordingDurationMs,
            };

            var pipeline = _pipelineFactory.CreateNotePipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            // Notify ControlPanel of pipeline completion (for telemetry)
            _controlPanel.OnPipelineCompleted(this, result);

            if (result.IsSuccess)
            {
                // result.Text contains the note content that was appended
                Log.Information("Note: Saved to {FilePath}", options.NotesFilePath);
                _notifications.ShowToast(_loc.GetString("Loading_NoteSaved_Title"), _loc.GetFormatted("Loading_NoteSaved_Message", System.IO.Path.GetFileName(options.NotesFilePath)), NotificationType.Success);
            }
            else
            {
                Log.Warning("Note: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Note pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }
}
