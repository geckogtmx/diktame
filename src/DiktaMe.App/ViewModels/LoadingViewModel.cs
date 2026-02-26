
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.App.Services;
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

    [ObservableProperty] private string _statusText = "Initializing...";
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
        PipelineConfigManager pipelines)
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
    }

    public async Task InitializeAsync()
    {
        try
        {
            // Step 1: Load settings
            StatusText = "Loading settings...";
            Progress = 0;
            await _settings.LoadAsync();
            Progress = 25;

            // Step 2: Initialize database
            StatusText = "Initializing database...";
            await _history.InitAsync();
            Progress = 50;

            // Step 3: Load snippets
            StatusText = "Loading snippets...";
            await _snippets.LoadAsync();
            Progress = 75;

            // Step 4: Check Ollama (if configured as local LLM)
            StatusText = "Checking local services...";
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

            // Step 5: Start hotkey manager and register hotkeys
            StatusText = "Registering hotkeys...";
            InitializeHotkeys();
            Progress = 100;

            StatusText = "Ready";
        }
        catch (Exception ex)
        {
            StatusText = "Initialization error — starting with defaults";
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
            "Hotkey Conflict",
            $"{e.Id} hotkey ({e.HotkeyString}) is already in use by another application",
            NotificationType.Error);
    }

    // ── Helper Methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Records audio from the configured input device using event-based async,
    /// with audio ducking and user notifications.
    /// </summary>
    private Task<string?> RecordAudioAsync(string mode)
    {
        var tcs = new TaskCompletionSource<string?>();

        // Get fresh recorder instance
        _currentRecorder?.Dispose();
        _currentRecorder = App.Current.Services.GetRequiredService<AudioRecorder>();

        // Subscribe to both stop events (auto-stop on duration limit, manual stop on toggle)
        EventHandler<RecordingStoppedEventArgs>? stopHandler = null;
        stopHandler = (_, args) =>
        {
            _currentRecorder!.AutoStopped -= stopHandler;
            _currentRecorder!.RecordingStopped -= stopHandler;
            _isRecording = false;
            tcs.TrySetResult(args.FilePath);
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
        _notifications.ShowToast("Recording", $"{mode} - speak now...", NotificationType.Info);

        return tcs.Task;
    }

    // ── Pipeline Handlers ─────────────────────────────────────────────────────

    private async Task RunDictationPipelineAsync()
    {
        try
        {
            Log.Information("Starting Dictate pipeline...");

            // Record audio (waits for auto-stop event)
            string? audioFile = await RecordAudioAsync("Dictate");
            if (audioFile == null)
            {
                Log.Warning("Dictate: No audio file produced");
                _notifications.ShowToast("Error", "Recording failed", NotificationType.Error);
                return;
            }

            // Stop ducking
            _audioDucker.Restore();

            Log.Information("Dictate: Recording complete, processing...");

            // Get active profile from CRUD system (use first available mode)
            var modes = _dictationModes.GetAllModes();
            if (modes.Count == 0)
            {
                Log.Warning("Dictate: No dictation modes configured");
                _notifications.ShowToast("Error", "No dictation modes configured", NotificationType.Error);
                return;
            }

            DictationProfile profile = _dictationModes.GetActiveProfile(modes[0].Id);

            // Build DictationOptions with all required fields
            var options = new DictationOptions
            {
                SystemPrompt = profile.UseLlm ? profile.SystemPrompt : null,
                RawMode = !profile.UseLlm,
                ModelName = profile.ModelName, // J.5: Per-mode model selection
                Language = _settings.Current.General.Language,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = _settings.Current.General.TrailingSpace,
                    AdditionalKey = string.IsNullOrEmpty(_settings.Current.General.AdditionalKey)
                        ? null
                        : _settings.Current.General.AdditionalKey,
                },
            };

            // Create pipeline and run with correct signature
            var pipeline = _pipelineFactory.CreateDictationPipeline();
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            // Access correct PipelineResult properties
            if (result.IsSuccess)
            {
                Log.Information("Dictate: Success, {Chars} chars injected", result.Text.Length);
                _notifications.PlaySound(NotificationType.Success);
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
            _notifications.ShowToast("Error", "Dictation failed", NotificationType.Error);
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
        try
        {
            Log.Information("Starting Refine pipeline...");

            // Record audio (waits for auto-stop event)
            string? audioFile = await RecordAudioAsync("Refine");
            if (audioFile == null)
            {
                Log.Warning("Refine: No audio file produced");
                _notifications.ShowToast("Error", "Recording failed", NotificationType.Error);
                return;
            }

            // Stop ducking
            _audioDucker.Restore();

            Log.Information("Refine: Recording complete, processing...");

            // Get active profile from CRUD system
            UtilityProfile profile = _pipelines.GetActiveProfile("refine");

            // Build RefineOptions with all required fields
            var options = new RefineOptions
            {
                SystemPrompt = profile.SystemPrompt ?? PromptDefaults.Refine, // fallback to default
                ModelName = profile.ModelName, // J.5: Per-mode model selection
                Language = _settings.Current.General.Language,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = _settings.Current.General.TrailingSpace,
                    AdditionalKey = string.IsNullOrEmpty(_settings.Current.General.AdditionalKey)
                        ? null
                        : _settings.Current.General.AdditionalKey,
                },
            };

            var pipeline = _pipelineFactory.CreateRefinePipeline();
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            if (result.IsSuccess)
            {
                Log.Information("Refine: Success, {Chars} chars", result.Text.Length);
                _notifications.PlaySound(NotificationType.Success);
            }
            else
            {
                Log.Warning("Refine: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Refine pipeline failed");
            _notifications.ShowToast("Error", "Refine failed", NotificationType.Error);
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
            string? audioFile = await RecordAudioAsync("Ask");
            if (audioFile == null)
            {
                Log.Warning("Ask: No audio file produced");
                _notifications.ShowToast("Error", "Recording failed", NotificationType.Error);
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
            };

            var pipeline = _pipelineFactory.CreateAskPipeline();
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            if (result.IsSuccess)
            {
                Log.Information("Ask: Answer = {Answer}", result.Text);
                // Show answer to user (Ask mode doesn't inject text, it returns answer)
                _notifications.ShowToast("Answer", result.Text, NotificationType.Success);
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
            _notifications.ShowToast("Error", "Ask failed", NotificationType.Error);
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
            string? audioFile = await RecordAudioAsync("Translate");
            if (audioFile == null)
            {
                Log.Warning("Translate: No audio file produced");
                _notifications.ShowToast("Error", "Recording failed", NotificationType.Error);
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
            };

            var pipeline = _pipelineFactory.CreateTranslatePipeline();
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            if (result.IsSuccess)
            {
                Log.Information("Translate: Success, {Chars} chars", result.Text.Length);
                _notifications.PlaySound(NotificationType.Success);
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
            _notifications.ShowToast("Error", "Translate failed", NotificationType.Error);
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
            string? audioFile = await RecordAudioAsync("Note");
            if (audioFile == null)
            {
                Log.Warning("Note: No audio file produced");
                _notifications.ShowToast("Error", "Recording failed", NotificationType.Error);
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
            };

            var pipeline = _pipelineFactory.CreateNotePipeline();
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            if (result.IsSuccess)
            {
                // result.Text contains the note content that was appended
                Log.Information("Note: Saved to {FilePath}", options.NotesFilePath);
                _notifications.ShowToast("Note Saved", $"Appended to {System.IO.Path.GetFileName(options.NotesFilePath)}", NotificationType.Success);
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
            _notifications.ShowToast("Error", "Note failed", NotificationType.Error);
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }
}
