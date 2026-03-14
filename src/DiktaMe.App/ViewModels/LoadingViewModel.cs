
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.App.Services;
using DiktaMe.Core.Account;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.STT;
using DiktaMe.Core.SystemManagement;
using DiktaMe.Core.TTS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Serilog;

namespace DiktaMe.App.ViewModels;
public sealed partial class LoadingViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly HistoryManager _history;
    private readonly ConversationManager _conversations;
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
    private readonly WhisperProvider _whisper;
    private readonly OllamaProvider _ollamaProvider;
    private readonly ILLMProviderFactory _llmFactory;
    private readonly ITtsPlayerService _ttsPlayer;
    private readonly TtsSpeaker _ttsSpeaker;

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
        ConversationManager conversations,
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
        LocalizationService loc,
        WhisperProvider whisper,
        OllamaProvider ollamaProvider,
        ILLMProviderFactory llmFactory,
        ITtsPlayerService ttsPlayer,
        TtsSpeaker ttsSpeaker)
    {
        _settings = settings;
        _history = history;
        _conversations = conversations;
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
        _whisper = whisper;
        _ollamaProvider = ollamaProvider;
        _llmFactory = llmFactory;
        _ttsPlayer = ttsPlayer;
        _ttsSpeaker = ttsSpeaker;
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
            await _conversations.InitAsync();
            Progress = 50;

            // Step 3: Load snippets
            StatusText = _loc.GetString("Loading_Snippets");
            await _snippets.LoadAsync();
            Progress = 75;

            // Step 4a: Download Whisper model if STT is local and model not present
            string sttProvider = GetActiveProvider("SttProvider");
            if (string.Equals(sttProvider, "whisper", StringComparison.OrdinalIgnoreCase)
                && !_whisper.IsModelDownloaded)
            {
                StatusText = _loc.GetString("Loading_DownloadingWhisper");
                try
                {
                    _whisper.DownloadProgress += (_, e) =>
                    {
                        StatusText = string.Create(System.Globalization.CultureInfo.InvariantCulture,
                            $"Downloading Whisper model... {e.Percent:F0}%");
                    };
                    await _whisper.DownloadModelAsync();
                    Log.Information("Whisper model downloaded successfully");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Whisper model download failed during loading");
                    _notifications.ShowToast(
                        _loc.GetString("Loading_WhisperFailed_Title"),
                        _loc.GetString("Loading_WhisperFailed_Message"),
                        NotificationType.Error);
                }
            }

            // Step 4b: Check Ollama + warmup if LLM is local
            StatusText = _loc.GetString("Loading_LocalServices");
            string llmProvider = GetActiveProvider("LlmProvider");
            OllamaCheckResult? ollamaResult = null;
            try
            {
                ollamaResult = await _ollama.CheckAsync(_settings.Current.OllamaModel);
            }
            catch (Exception ex)
            {
                // Non-fatal — Ollama may not be installed
                Log.Debug(ex, "Ollama check skipped during loading");
            }

            // Auto-start Ollama if user's LLM is local but Ollama isn't running
            if (string.Equals(llmProvider, "ollama", StringComparison.OrdinalIgnoreCase)
                && (ollamaResult is null || ollamaResult.Status == OllamaStatus.Offline))
            {
                StatusText = _loc.GetString("Loading_StartingOllama");
                Log.Information("Loading: Ollama offline, attempting auto-start");
                bool started = await _ollama.StartOllamaAsync();
                if (started)
                {
                    try
                    {
                        ollamaResult = await _ollama.CheckAsync(_settings.Current.OllamaModel);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Loading: Ollama re-check failed after auto-start");
                    }
                }
                else
                {
                    Log.Warning("Loading: Ollama auto-start failed — local LLM will be unavailable");
                }
            }

            // E2E warmup: warm both Whisper and the factory-cached Ollama provider
            // to eliminate cold-start penalty on first dictation (SPEC_011 §11).
            // Controlled by OllamaAutoWarmup setting. Only warms local providers.
            if (_settings.Current.OllamaAutoWarmup)
            {
                await RunE2EWarmupAsync(sttProvider, llmProvider, ollamaResult);
            }
            else if (string.Equals(llmProvider, "ollama", StringComparison.OrdinalIgnoreCase)
                     && ollamaResult?.Status == OllamaStatus.Ready)
            {
                // Lightweight fallback: at minimum warm the DI singleton so the model
                // is loaded in Ollama's context (even if factory instance is cold).
                StatusText = _loc.GetString("Loading_WarmingOllama");
                try
                {
                    await _ollamaProvider.WarmUpAsync();
                    LogOllamaGpuAssessment(_ollamaProvider);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Ollama warmup failed during loading");
                }
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

            // Unsubscribe first to prevent double-subscription when LoadingWindow
            // is re-created after wizard completion (singleton ViewModel, new Window).
            _hotkeyManager.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyManager.RegistrationFailed -= OnHotkeyRegistrationFailed;

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

        success = _hotkeyManager.Register(HotkeyId.ReadSelection, hotkeys.ReadSelection);
        Log.Debug("Register ReadSelection ({Hotkey}): {Success}", hotkeys.ReadSelection, success);
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

                // Stop active TTS playback if any hotkey is pressed while speaking
                if (_ttsPlayer.IsPlaying)
                {
                    Log.Information("Hotkey {Id}: stopping active TTS playback", e.Id);
                    _ttsPlayer.Stop();
                    // ReadSelection toggle-stop: just stop, don't restart
                    if (e.Id == HotkeyId.ReadSelection)
                    {
                        return;
                    }
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
                        _ = Task.Run(() =>
                        {
                            _textInjector.ReInjectLast();
                            var sound = _settings.Current.Sound ?? new();
                            _notifications.PlayCustomSound(sound.StopSound);
                        });
                        break;

                    case HotkeyId.ReadSelection:
                        _ = RunReadSelectionPipelineAsync();
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

    // ── E2E Warmup (SPEC_011 §11) ──────────────────────────────────────────

    /// <summary>
    /// Full production-path warmup: warms both Whisper (model load + Vulkan shaders)
    /// and the factory-cached Ollama provider (HTTP connection + model context).
    /// Eliminates cold-start penalty on first dictation.
    /// </summary>
    private async Task RunE2EWarmupAsync(
        string sttProvider, string llmProvider, OllamaCheckResult? ollamaResult)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long whisperMs = 0, llmMs = 0;

        StatusText = _loc.GetString("Loading_E2EWarmup");

        // 1. Whisper warmup: transcribe a tiny silent WAV to force model load + shader compile
        if (string.Equals(sttProvider, "whisper", StringComparison.OrdinalIgnoreCase)
            && _whisper.IsModelDownloaded)
        {
            try
            {
                var whisperSw = System.Diagnostics.Stopwatch.StartNew();
                string silentWav = GenerateSilentWav();
                try
                {
                    await _whisper.TranscribeAsync(silentWav, "en");
                }
                finally
                {
                    try { File.Delete(silentWav); } catch { /* best-effort cleanup */ }
                }
                whisperMs = whisperSw.ElapsedMilliseconds;
                Log.Information("E2E warmup: Whisper primed in {Ms}ms", whisperMs);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "E2E warmup: Whisper warmup failed (non-fatal)");
            }
        }

        // 2. LLM warmup via factory: get-or-create the production-path cached provider
        //    and send a minimal prompt to prime the HTTP connection + Ollama model context.
        if (string.Equals(llmProvider, "ollama", StringComparison.OrdinalIgnoreCase)
            && ollamaResult?.Status == OllamaStatus.Ready)
        {
            try
            {
                var llmSw = System.Diagnostics.Stopwatch.StartNew();
                var factoryProvider = _llmFactory.CreateProvider("ollama",
                    model: _settings.Current.OllamaModel);

                await factoryProvider.ProcessAsync("Hi", "You are a text formatter. Output only the result.", "warmup");
                llmMs = llmSw.ElapsedMilliseconds;

                // GPU assessment from the factory provider
                if (factoryProvider is OllamaProvider ollamaFP)
                {
                    LogOllamaGpuAssessment(ollamaFP);
                }

                Log.Information("E2E warmup: LLM (factory) primed in {Ms}ms", llmMs);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "E2E warmup: LLM warmup failed (non-fatal)");
            }
        }

        sw.Stop();
        Log.Information("E2E warmup complete: Whisper {WhisperMs}ms, LLM {LlmMs}ms, total {TotalMs}ms",
            whisperMs, llmMs, sw.ElapsedMilliseconds);
    }

    /// <summary>Generates a 0.5-second silent WAV file in temp for Whisper warmup.</summary>
    private static string GenerateSilentWav()
    {
        string path = Path.Combine(Path.GetTempPath(), $"diktame_warmup_{Guid.NewGuid():N}.wav");
        int sampleRate = 16000;
        int channels = 1;
        int bitsPerSample = 16;
        int durationSamples = sampleRate / 2; // 0.5 seconds
        int dataSize = durationSamples * channels * (bitsPerSample / 8);

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // WAV header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);
        bw.Write("fmt "u8);
        bw.Write(16); // chunk size
        bw.Write((short)1); // PCM
        bw.Write((short)channels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * channels * (bitsPerSample / 8)); // byte rate
        bw.Write((short)(channels * (bitsPerSample / 8))); // block align
        bw.Write((short)bitsPerSample);
        bw.Write("data"u8);
        bw.Write(dataSize);
        bw.Write(new byte[dataSize]); // silence = all zeros

        return path;
    }

    private static void LogOllamaGpuAssessment(OllamaProvider provider)
    {
        double? toksPerSec = provider.LastTokensPerSec;
        if (toksPerSec.HasValue)
        {
            string assessment = toksPerSec.Value > 50 ? "GPU"
                : toksPerSec.Value < 20 ? "CPU"
                : "BORDERLINE";
            Log.Information("Ollama warmup: {TokSec:F1} tok/s ({Assessment})",
                toksPerSec.Value, assessment);
        }
        else
        {
            Log.Information("Ollama warmup completed (tok/s not reported)");
        }
    }

    /// <summary>
    /// Reads the active provider name (STT or LLM) from ModeProfiles for the dictate mode.
    /// Always reads profile 0, which is the canonical copy written by the wizard and Settings UI.
    /// </summary>
    private string GetActiveProvider(string propertyName)
    {
        if (!_settings.Current.ModeProfiles.TryGetValue("dictate_0", out var ms))
        {
            return string.Empty;
        }

        return propertyName switch
        {
            "SttProvider" => ms.SttProvider,
            "LlmProvider" => ms.LlmProvider,
            _ => string.Empty,
        };
    }

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
                RawMode = _controlPanel.IsLlmOff || !profile.UseLlm,
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
                if (result.WarningMessage is not null)
                {
                    _notifications.ShowToast("Warning", result.WarningMessage, NotificationType.Warning);
                }
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

            // TTS before DB write so we can capture timing
            long ttsMs = 0;
            if (result.IsSuccess)
            {
                ttsMs = await _ttsSpeaker.SpeakIfEnabledAsync(
                    result.Text, "ask", _recordingCts?.Token ?? CancellationToken.None);
                if (ttsMs > 0)
                {
                    Log.Information("Ask: TTS played in {TtsMs}ms", ttsMs);
                }
            }

            // Notify ControlPanel of pipeline completion (for telemetry)
            _controlPanel.OnPipelineCompleted(this,
                ttsMs > 0 ? result with { TtsPlayedMs = ttsMs } : result);

            if (result.IsSuccess)
            {
                Log.Information("Ask: Answer = {Answer}", result.Text);

                // Route output based on user preference
                // Suppress notification TTS if the answer was already spoken by Ask TTS
                bool alreadySpoken = ttsMs > 0;
                AskOutputMode outputMode = _settings.Current.General.AskOutput;
                switch (outputMode)
                {
                    case AskOutputMode.ToastOnly:
                        _notifications.ShowToast("Answer", result.Text, NotificationType.Success, suppressTts: alreadySpoken);
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
                        _notifications.ShowToast("Answer (copied)", result.Text, NotificationType.Success, suppressTts: alreadySpoken);
                        break;

                    default:
                        _notifications.ShowToast("Answer", result.Text, NotificationType.Success, suppressTts: alreadySpoken);
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

            // TTS before DB write so we can capture timing
            long ttsMs = 0;
            if (result.IsSuccess)
            {
                ttsMs = await _ttsSpeaker.SpeakIfEnabledAsync(
                    result.Text, "translate", _recordingCts?.Token ?? CancellationToken.None);
                if (ttsMs > 0)
                {
                    Log.Information("Translate: TTS played in {TtsMs}ms", ttsMs);
                }
            }

            // Notify ControlPanel of pipeline completion (for telemetry)
            _controlPanel.OnPipelineCompleted(this,
                ttsMs > 0 ? result with { TtsPlayedMs = ttsMs } : result);

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

    // ── Read Selection (SPEC_003 Phase C) ─────────────────────────────────────

    private async Task RunReadSelectionPipelineAsync()
    {
        try
        {
            Log.Information("Starting ReadSelection pipeline...");

            // Check if TTS is enabled
            var ttsSettings = _settings.Current.Tts;
            if (!ttsSettings.Enabled)
            {
                Log.Information("ReadSelection: TTS is disabled");
                _notifications.ShowToast(
                    _loc.GetString("ReadSelection_Disabled_Title"),
                    _loc.GetString("ReadSelection_Disabled_Message"),
                    NotificationType.Warning);
                return;
            }

            // Play utility sound to acknowledge hotkey
            var soundSettings = _settings.Current.Sound ?? new();
            _notifications.PlayCustomSound(soundSettings.UtilitySound);

            // Capture selection (runs on background thread to avoid blocking UI)
            string? selectedText = await Task.Run(() => _textInjector.CaptureSelection());

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                Log.Warning("ReadSelection: no text selected");
                _notifications.ShowToast(
                    _loc.GetString("ReadSelection_NoSelection_Title"),
                    _loc.GetString("ReadSelection_NoSelection_Message"),
                    NotificationType.Warning);
                return;
            }

            Log.Information("ReadSelection: captured {Chars} chars", selectedText.Length);

            // Start audio ducking if enabled
            if (ttsSettings.DuckDuringPlayback && _settings.Current.AudioDucking.Enabled)
            {
                _audioDucker.IsEnabled = true;
                _audioDucker.DuckLevel = _settings.Current.AudioDucking.DuckLevelPercent / 100f;
                _audioDucker.Duck();
            }

            // Create and run pipeline
            var pipeline = _pipelineFactory.CreateReadSelectionPipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;

            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(selectedText, _recordingCts.Token);

            // Notify ControlPanel of pipeline completion (for telemetry)
            _controlPanel.OnPipelineCompleted(this, result);

            if (result.IsSuccess)
            {
                Log.Information("ReadSelection: complete ({TotalMs}ms)", result.TotalMs);
            }
            else
            {
                Log.Warning("ReadSelection: failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "TTS failed", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ReadSelection pipeline failed");
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
