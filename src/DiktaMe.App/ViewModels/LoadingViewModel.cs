using System.Runtime.InteropServices.WindowsRuntime;

using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.App.Services;
using DiktaMe.Core.Account;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.Security;
using DiktaMe.Core.STT;
using DiktaMe.Core.SystemManagement;
using DiktaMe.Core.TTS;
using DiktaMe.Core.Vision;
using DiktaMe.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Serilog;
using Windows.Storage.Streams;


namespace DiktaMe.App.ViewModels;
public sealed partial class LoadingViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly HistoryManager _history;
    private readonly ConversationManager _conversations;
    private readonly WalletManager _wallet;
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
    private readonly LocalizationService _loc;
    private readonly WhisperProvider _whisper;
    private readonly OllamaProvider _ollamaProvider;
    private readonly ILLMProviderFactory _llmFactory;
    private readonly ITtsPlayerService _ttsPlayer;
    private readonly TtsSpeaker _ttsSpeaker;
    private readonly AudioLevelMonitor _levelMonitor;
    private readonly MuteDetector _muteDetector;
    private readonly WalletGeminiProxy _walletProxy;
    private readonly PipelineEventBus _pipelineEventBus;
    private readonly PluginManager _pluginManager;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private double _progress;

    private AudioRecorder? _currentRecorder;
    private CancellationTokenSource? _recordingCts;
    private DispatcherQueue? _uiDispatcher;
    private bool _isRecording;

    /// <summary>
    /// Temporary vision telemetry data set before RunVisionPipelineCoreAsync, consumed by the logging enrichment.
    /// </summary>
    private (string CaptureMode, string ActionType, int ImageWidth, int ImageHeight, long CaptureMs)? _pendingVisionTelemetry;

    /// <summary>
    /// Transient per-trigger mode override for Stream Deck per-button modes.
    /// Null = use app's active mode. Set by <see cref="TriggerPipeline"/>, consumed by pipeline methods.
    /// </summary>
    private string? _modeIdOverride;

    public event Action? LoadingComplete;

    public LoadingViewModel(
        SettingsManager settings,
        HistoryManager history,
        ConversationManager conversations,
        WalletManager wallet,
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
        LocalizationService loc,
        WhisperProvider whisper,
        OllamaProvider ollamaProvider,
        ILLMProviderFactory llmFactory,
        ITtsPlayerService ttsPlayer,
        TtsSpeaker ttsSpeaker,
        AudioLevelMonitor levelMonitor,
        MuteDetector muteDetector,
        WalletGeminiProxy walletProxy,
        PipelineEventBus pipelineEventBus,
        PluginManager pluginManager)
    {
        _settings = settings;
        _history = history;
        _conversations = conversations;
        _wallet = wallet;
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
        _loc = loc;
        _whisper = whisper;
        _ollamaProvider = ollamaProvider;
        _llmFactory = llmFactory;
        _ttsPlayer = ttsPlayer;
        _ttsSpeaker = ttsSpeaker;
        _levelMonitor = levelMonitor;
        _muteDetector = muteDetector;
        _walletProxy = walletProxy;
        _pipelineEventBus = pipelineEventBus;
        _pluginManager = pluginManager;
        _statusText = _loc.GetString("Loading_Initializing");

        // Subscribe to vision row events from CP bar
        _controlPanel.VisionCaptureRequested += OnVisionCaptureRequested;
        _controlPanel.VisionActionChosen += OnVisionActionChosen;
        _controlPanel.VisionExited += (_, _) => DismissDimOverlay();
        _controlPanel.VisionDefaultOverridden += OnVisionDefaultOverridden;
        _controlPanel.RecordingStopRequested += OnRecordingStopRequested;
        _controlPanel.RecordingPauseToggleRequested += OnRecordingPauseToggleRequested;
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

            // Step 2: Initialize databases
            StatusText = _loc.GetString("Loading_Database");
            await _history.InitAsync();
            await _conversations.InitAsync();
            await _wallet.InitAsync();
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
                        NotificationType.Error,
                        spokenKey: "Loading_WhisperFailed");
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
                await RunLightweightOllamaWarmupAsync();
            }
            Progress = 85;

            // Step 5: Refresh JWT + sync wallet balance + profile (if signed in)
            if (_accountService.HasValidToken)
            {
                StatusText = _loc.GetString("Loading_Account");

                // Ensure JWT is fresh before any wallet/profile calls
                var tokenRefresh = App.Current.Services.GetRequiredService<TokenRefreshService>();
                await tokenRefresh.CheckAndRefreshAsync();
                tokenRefresh.Start();

                await SyncWalletBalanceAsync();

                // Sync server-side profile (picks up avatar changes from website)
                await _accountService.SyncProfileFromServerAsync();
            }

            // Re-validate Power License online (non-blocking — offline grace on failure)
            var licenseManager = App.Current.Services.GetRequiredService<DiktaMe.Core.Security.LicenseManager>();
            await licenseManager.ValidateAsync();

            // Wire post-pipeline balance updates from wallet proxy events
            WireWalletBalanceEvents();
            Progress = 90;

            // Step 6: Start hotkey manager and register hotkeys
            StatusText = _loc.GetString("Loading_Hotkeys");
            InitializeHotkeys();
            Progress = 95;

            // Step 7: Discover and load plugins (no-op if plugins/ is empty)
            await DiscoverPluginsAsync();
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

    private async Task DiscoverPluginsAsync()
    {
        try
        {
            var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
            var context = new PluginContext
            {
                Services = App.Current.Services,
                PipelineEvents = _pipelineEventBus,
                Settings = new JsonPluginSettingsStore("_host"),
                UI = App.Current.Services.GetRequiredService<PluginUIRegistry>(),
                Dispatcher = action => _uiDispatcher?.TryEnqueue(() => action()),
                Logger = Log.ForContext<PluginManager>(),
            };
            await _pluginManager.DiscoverAndLoadAsync(pluginsDir, context);
            Log.Information("Plugin discovery complete: {Count} plugin(s) loaded", _pluginManager.Plugins.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Plugin discovery failed — continuing without plugins");
        }
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

        if (_settings.Current.Vision.Enabled)
        {
            success = _hotkeyManager.Register(HotkeyId.Vision, hotkeys.Vision);
            Log.Debug("Register Vision ({Hotkey}): {Success}", hotkeys.Vision, success);
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        // Capture the foreground window IMMEDIATELY on the hotkey thread —
        // before TryEnqueue, which may delay and allow focus to shift.
        IntPtr sourceWindow = TextInjector.GetCurrentForegroundWindow();
        Log.Information("Hotkey pressed: {Id} (sourceHwnd=0x{Hwnd:X})", e.Id, sourceWindow);

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
                // Block all hotkeys during video recording (except Vision which could stop it in the future)
                if (_controlPanel.VisionPhase == VisionWizardStep.Recording && e.Id != HotkeyId.Vision)
                {
                    Log.Information("Hotkey {Id}: blocked — video recording in progress", e.Id);
                    return;
                }

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
                        _ = RunRefinePipelineAsync(sourceWindow);
                        break;

                    case HotkeyId.Ask:
                        _ = RunAskPipelineAsync();
                        break;

                    case HotkeyId.Translate:
                        _ = RunTranslatePipelineAsync();
                        break;

                    case HotkeyId.Note:
                        _ = RunNotePipelineAsync(sourceWindow);
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
                        _ = RunReadSelectionPipelineAsync(sourceWindow);
                        break;

                    case HotkeyId.Vision:
                        EnterVisionMode();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling hotkey {Id}", e.Id);
            }
        });
    }

    /// <summary>
    /// Public entry point for external IPC triggers (Stream Deck, automation scripts).
    /// Maps a pipeline type string to the existing private pipeline methods.
    /// Must be called on the UI thread (callers should dispatch via DispatcherQueue first).
    /// </summary>
    /// <param name="pipelineType">
    /// One of: "dictate", "refine_auto", "refine_voice", "ask", "translate", "note", "oops", "read_selection".
    /// </param>
    /// <param name="modeId">Optional dictation mode ID override (only applies to "dictate").</param>
    /// <param name="sourceWindow">HWND of the foreground window, captured by the caller before UI dispatch.</param>
    public void TriggerPipeline(string pipelineType, string? modeId, IntPtr sourceWindow)
    {
        try
        {
            // Toggle-stop: if already recording, stop instead of starting a new pipeline
            if (_isRecording && _currentRecorder is not null)
            {
                Log.Information("IPC trigger {Type}: stopping active recording", pipelineType);
                _ = _currentRecorder.StopRecordingAsync();
                return;
            }

            // Stop active TTS playback
            if (_ttsPlayer.IsPlaying)
            {
                Log.Information("IPC trigger {Type}: stopping active TTS playback", pipelineType);
                _ttsPlayer.Stop();
                if (string.Equals(pipelineType, "read_selection", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // Set transient mode override (consumed by RunBatchDictationAsync / RunStreamingDictationAsync)
            _modeIdOverride = string.IsNullOrEmpty(modeId) ? null : modeId;

            switch (pipelineType.ToLowerInvariant())
            {
                case "dictate":
                    _ = RunDictationPipelineAsync();
                    break;

                case "refine_auto":
                    _ = RunRefineAutoAsync(sourceWindow);
                    break;

                case "refine_voice":
                    _ = RunRefineVoiceAsync(sourceWindow);
                    break;

                case "ask":
                    _ = RunAskPipelineAsync();
                    break;

                case "translate":
                    _ = RunTranslatePipelineAsync();
                    break;

                case "note":
                    _ = RunNotePipelineAsync(sourceWindow);
                    break;

                case "oops":
                    _ = Task.Run(() =>
                    {
                        _textInjector.ReInjectLast();
                        var sound = _settings.Current.Sound ?? new();
                        _notifications.PlayCustomSound(sound.StopSound);
                    });
                    break;

                case "read_selection":
                    _ = RunReadSelectionPipelineAsync(sourceWindow);
                    break;

                default:
                    Log.Warning("IPC trigger: unknown pipeline type '{Type}'", pipelineType);
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "IPC trigger failed for pipeline type '{Type}'", pipelineType);
        }
    }

    private void OnHotkeyRegistrationFailed(object? sender, HotkeyRegistrationFailedEventArgs e)
    {
        Log.Warning("Hotkey registration failed: {Id} = '{HotkeyString}' - {Reason}",
            e.Id, e.HotkeyString, e.Reason);

        _notifications.ShowToast(
            _loc.GetString("Loading_HotkeyConflict_Title"),
            _loc.GetFormatted("Loading_HotkeyConflict_Message", e.Id, e.HotkeyString),
            NotificationType.Error,
            spokenKey: "Loading_HotkeyConflict",
            spokenArgs: [e.Id]);
    }

    private void OnMuteStateChanged(object? sender, MuteStateChangedEventArgs e)
    {
        if (e.IsMuted && _isRecording)
        {
            _notifications.ShowToast(
                _loc.GetString("Recording_MicMuted_Title"),
                _loc.GetString("Recording_MicMuted_Message"),
                NotificationType.Warning,
                spokenKey: "Recording_MicMuted");
        }
    }

    // ── Helper Methods ────────────────────────────────────────────────────────

    // ── E2E Warmup (SPEC_011 §11) ──────────────────────────────────────────

    /// <summary>
    /// Lightweight warmup: warm only the user's configured LLM model via factory.
    /// Used when OllamaAutoWarmup is off but Ollama is ready.
    /// </summary>
    private async Task RunLightweightOllamaWarmupAsync()
    {
        StatusText = _loc.GetString("Loading_WarmingOllama");
        try
        {
            string activeModel = GetActiveLlmModel();
            Log.Debug("Ollama lightweight warmup: model '{Model}'", activeModel);
            var factoryProvider = _llmFactory.CreateProvider("ollama", model: activeModel);
            if (factoryProvider is OllamaProvider ollamaWarmup)
            {
                await ollamaWarmup.WarmUpAsync();
                LogOllamaGpuAssessment(ollamaWarmup);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Ollama warmup failed during loading");
        }
    }

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
                string activeModel = GetActiveLlmModel();
                Log.Debug("E2E warmup: warming active LLM model '{Model}'", activeModel);
                var factoryProvider = _llmFactory.CreateProvider("ollama",
                    model: activeModel);

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

    // ── Wallet Sync ─────────────────────────────────────────────────────────

    /// <summary>
    /// Sync wallet balance from server (wallet-status Edge Function).
    /// Falls back to local ledger balance if cloud is unreachable.
    /// </summary>
    private async Task SyncWalletBalanceAsync()
    {
        try
        {
            var secureStorage = App.Current.Services.GetRequiredService<SecureStorage>();
            string? token = secureStorage.RetrieveKey("trial_token"); // backward compat key name

            if (!string.IsNullOrEmpty(token))
            {
                string statusUrl = _settings.Current.Account.WalletProxyUrl
                    .Replace("/wallet-proxy", "/wallet-status", StringComparison.Ordinal);

                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                using var request = new System.Net.Http.HttpRequestMessage(
                    System.Net.Http.HttpMethod.Get, statusUrl);
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                using var response = await http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("balance_micro", out var balProp))
                    {
                        long serverBalance = balProp.GetInt64();
                        await _wallet.SyncBalanceAsync(serverBalance);
                        await CacheWalletBalanceAsync(serverBalance);
                        Log.Information("Wallet: cloud sync succeeded, balance = {Balance}µ$", serverBalance);
                        return;
                    }
                }
                else
                {
                    Log.Warning("Wallet: cloud sync returned {StatusCode}, using local balance",
                        (int)response.StatusCode);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Wallet: cloud sync failed, using local balance");
        }

        // Fallback: use local ledger balance
        long localBalance = await _wallet.GetBalanceMicroAsync();
        await CacheWalletBalanceAsync(localBalance);
        Log.Information("Wallet: local balance cached at startup = {Balance}µ$", localBalance);
    }

    /// <summary>
    /// Caches the wallet balance in settings for HUD display.
    /// </summary>
    private async Task CacheWalletBalanceAsync(long balanceMicro)
    {
        var updated = _settings.Current with
        {
            Account = _settings.Current.Account with { WalletBalanceMicro = balanceMicro },
        };
        await _settings.UpdateAsync(updated);
    }

    /// <summary>
    /// Public entry point for post-sign-in wallet sync.
    /// Called from App.xaml.cs HandleDeepLink after auth callback completes.
    /// </summary>
    public async Task SyncWalletAfterSignInAsync()
    {
        await SyncWalletBalanceAsync().ConfigureAwait(false);

        // Sync server-side profile (picks up avatar_url for email/password users)
        await _accountService.SyncProfileFromServerAsync().ConfigureAwait(false);

        // Refresh ControlPanel HUD on UI thread
        _uiDispatcher?.TryEnqueue(() =>
        {
            _controlPanel.LoadFromSettings(_settings.Current);

            // Show toast confirming sign-in
            string email = _accountService.Email ?? "Unknown";
            _notifications.ShowToast("Sign In", $"Signed in as {email}", suppressTts: true);
        });
    }

    /// <summary>
    /// Subscribes to the WalletGeminiProxy.BalanceUpdated event to keep the
    /// HUD balance badge in sync after each proxy response.
    /// </summary>
    private void WireWalletBalanceEvents()
    {
        _walletProxy.BalanceUpdated += balanceMicro =>
        {
            // Fire-and-forget: update settings cache + tell ControlPanel to refresh
            _ = Task.Run(async () =>
            {
                try
                {
                    await _wallet.SyncBalanceAsync(balanceMicro);
                    await CacheWalletBalanceAsync(balanceMicro);

                    // Refresh ControlPanel HUD on UI thread
                    _uiDispatcher?.TryEnqueue(() => _controlPanel.LoadFromSettings(_settings.Current));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Wallet: failed to update balance after proxy response");
                }
            });
        };

        // Subscribe to session expiry — attempt refresh before showing error
        var walletStt = App.Current.Services.GetRequiredService<WalletDeepgramProxy>();
        var tokenRefresh = App.Current.Services.GetRequiredService<TokenRefreshService>();

        void HandleSessionExpired()
        {
            _ = Task.Run(async () =>
            {
                bool refreshed = await tokenRefresh.TryRefreshAsync().ConfigureAwait(false);
                if (!refreshed)
                {
                    _uiDispatcher?.TryEnqueue(() =>
                    {
                        _notifications.ShowToast("Session Expired", "Please sign in again.", suppressTts: true);
                    });
                }
            });
        }

        walletStt.SessionExpired += HandleSessionExpired;
        _walletProxy.SessionExpired += HandleSessionExpired;
        tokenRefresh.SessionExpired += HandleSessionExpired;
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
    /// Gets the LLM model configured in the active dictation mode.
    /// Falls back to the global OllamaModel setting if no mode-specific model is set.
    /// </summary>
    private string GetActiveLlmModel()
    {
        if (_settings.Current.ModeProfiles.TryGetValue("dictate_0", out var ms)
            && !string.IsNullOrEmpty(ms.LlmModel))
        {
            return ms.LlmModel;
        }

        return _settings.Current.OllamaModel;
    }

    /// <summary>
    /// Records audio from the configured input device using event-based async,
    /// with audio ducking and user notifications.
    /// </summary>
    /// <param name="mode">Display name for logging/toast (e.g. "Dictate", "Ask").</param>
    /// <param name="isDictate">True for dictation modes (uses start/stop sounds), false for utility (uses utility sound).</param>
    /// <returns>A tuple containing the audio file path and recording duration in milliseconds.</returns>
    private async Task<(string? FilePath, long DurationMs)> RecordAudioAsync(string mode, bool isDictate)
    {
        var tcs = new TaskCompletionSource<(string?, long)>();

        // Get fresh recorder instance
        _currentRecorder?.Dispose();
        _currentRecorder = App.Current.Services.GetRequiredService<AudioRecorder>();

        // Capture sound settings for the stop handler (which fires on a background thread)
        var soundSettings = _settings.Current.Sound ?? new();
        string stopSound = isDictate ? soundSettings.StopSound : soundSettings.UtilitySound;

        // Wire audio level monitoring for visual effects
        _levelMonitor.Start();
        _currentRecorder.AudioDataAvailable += (_, e) =>
            _levelMonitor.UpdateLevel(e.PcmData, e.BytesRecorded);

        // Subscribe to both stop events (auto-stop on duration limit, manual stop on toggle)
        EventHandler<RecordingStoppedEventArgs>? stopHandler = null;
        stopHandler = (_, args) =>
        {
            _currentRecorder!.AutoStopped -= stopHandler;
            _currentRecorder!.RecordingStopped -= stopHandler;
            _isRecording = false;
            _levelMonitor.Stop();
            _muteDetector.Stop();
            _muteDetector.MuteStateChanged -= OnMuteStateChanged;
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
            _audioDucker.RampDownMs = _settings.Current.AudioDucking.RampDownMs;
            await _audioDucker.DuckAsync().ConfigureAwait(false);
        }

        // Start recording (all params are optional)
        _isRecording = true;
        _currentRecorder.StartRecording(
            deviceLabel: deviceLabel,
            deviceId: null, // let AudioDeviceManager resolve from label
            maxDurationSeconds: maxDuration);

        Log.Information("{Mode}: Recording started (max {MaxSec}s)", mode, maxDuration);

        // Mute detection: sync device label, check immediately, monitor changes
        _muteDetector.UpdateDeviceLabel(deviceLabel);
        if (_muteDetector.CheckMuteState() == true)
        {
            _notifications.ShowToast(
                _loc.GetString("Recording_MicMuted_Title"),
                _loc.GetString("Recording_MicMuted_Message"),
                NotificationType.Warning,
                spokenKey: "Recording_MicMuted");
        }
        _muteDetector.MuteStateChanged += OnMuteStateChanged;
        _muteDetector.Start();

        // Play start sound (after recording has begun)
        string startSound = isDictate ? soundSettings.StartSound : soundSettings.UtilitySound;
        _notifications.PlayCustomSound(startSound);

        return await tcs.Task;
    }

    // ── Pipeline Handlers ─────────────────────────────────────────────────────

    private async Task RunDictationPipelineAsync()
    {
        // Wallet mode: try streaming via Gemini Live API first.
        // Falls back to batch automatically on WalletStreamingFallbackException
        // (connection failure, killswitch active, insufficient balance, etc.).
        if (_settings.Current.AuthMode == AuthMode.Wallet)
        {
            await RunWalletStreamingDictationAsync();
            return;
        }

        // Non-wallet: standard streaming or batch based on provider capability
        if (_settings.Current.General.StreamingEnabled
            && _pipelineFactory.CanStreamDictation())
        {
            await RunStreamingDictationAsync();
        }
        else
        {
            await RunBatchDictationAsync();
        }
    }

    /// <summary>
    /// Runs the Wallet streaming dictation pipeline (Gemini Live API via Edge Function).
    /// On WalletStreamingFallbackException, silently falls back to the batch pipeline.
    /// The buffer-and-flush architecture in WalletStreamingSTTProxy ensures the beep
    /// and microphone start instantly, decoupled from the network handshake.
    /// </summary>
    private async Task RunWalletStreamingDictationAsync()
    {
        StreamingDictationPipeline? streamingPipeline = null;
        try
        {
            streamingPipeline = _pipelineFactory.CreateWalletStreamingDictationPipeline();
            if (streamingPipeline is null)
            {
                // Factory returned null — no streaming proxy registered or not Wallet mode
                Log.Information("WalletStreaming: pipeline unavailable, using batch");
                await RunBatchDictationAsync();
                return;
            }

            Log.Information("Starting Wallet Streaming Dictate pipeline (Gemini Live)...");

            streamingPipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            streamingPipeline.Completed += _controlPanel.OnPipelineCompleted;
            streamingPipeline.Completed += (_, result) => _pipelineEventBus.PublishCompleted(result);

            _currentRecorder?.Dispose();
            _currentRecorder = App.Current.Services.GetRequiredService<AudioRecorder>();

            _levelMonitor.Start();
            _currentRecorder.AudioDataAvailable += (_, e) =>
                _levelMonitor.UpdateLevel(e.PcmData, e.BytesRecorded);

            string? activeModeId = _modeIdOverride ?? _controlPanel.ActiveDictationModeId;
            DictationProfile? profile = activeModeId is not null
                ? _dictationModes.GetActiveProfile(activeModeId)
                : null;

            var options = new DictationOptions
            {
                RawMode = true,
                Language = _settings.Current.General.Language,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = profile?.TrailingSpace ?? true,
                },
            };

            if (_settings.Current.AudioDucking.Enabled)
            {
                _audioDucker.IsEnabled = true;
                _audioDucker.DuckLevel = _settings.Current.AudioDucking.DuckLevelPercent / 100f;
                _audioDucker.RampDownMs = _settings.Current.AudioDucking.RampDownMs;
                await _audioDucker.DuckAsync().ConfigureAwait(false);
            }

            var audio = _settings.Current.Audio;
            string? deviceLabel = string.IsNullOrEmpty(audio.DeviceName) ? null : audio.DeviceName;
            _isRecording = true;
            _currentRecorder.StartRecording(
                deviceLabel: deviceLabel,
                deviceId: null,
                maxDurationSeconds: audio.MaxDurationSeconds);

            _muteDetector.UpdateDeviceLabel(deviceLabel);
            if (_muteDetector.CheckMuteState() == true)
            {
                _notifications.ShowToast(
                    _loc.GetString("Recording_MicMuted_Title"),
                    _loc.GetString("Recording_MicMuted_Message"),
                    NotificationType.Warning,
                    spokenKey: "Recording_MicMuted");
            }
            _muteDetector.MuteStateChanged += OnMuteStateChanged;
            _muteDetector.Start();

            var soundSettings = _settings.Current.Sound ?? new();
            _notifications.PlayCustomSound(soundSettings.StartSound);

            _recordingCts = new CancellationTokenSource();
            var result = await streamingPipeline.RunAsync(
                _currentRecorder, options, _recordingCts.Token);

            _isRecording = false;
            _levelMonitor.Stop();
            _muteDetector.Stop();
            _muteDetector.MuteStateChanged -= OnMuteStateChanged;
            _notifications.PlayCustomSound(soundSettings.StopSound);

            if (result.IsSuccess)
            {
                Log.Information("WalletStreamingDictate: Success, {Chars} chars", result.Text.Length);
            }
            else
            {
                Log.Warning("WalletStreamingDictate: Failed — {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (DiktaMe.Core.Account.WalletStreamingFallbackException ex)
        {
            // Non-critical: streaming unavailable, fall back to batch transparently
            Log.Information("WalletStreaming: fallback to batch — {Reason}", ex.Message);
            await RunBatchDictationAsync();
        }
        catch (Exception ex)
        {
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Wallet Streaming Dictate pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
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

    private bool HandleLicenseError(Exception ex)
    {
        if (ex is InvalidOperationException && ex.Message.Contains("Power License", StringComparison.Ordinal))
        {
            Log.Warning("Pipeline blocked: no Power License");
            _notifications.ShowToast(
                "License Required",
                "Local AI providers need a Power License. Purchase at dikta.me",
                NotificationType.Warning,
                spokenKey: "License_Required");
            return true;
        }

        return false;
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
            streamingPipeline.Completed += (_, result) => _pipelineEventBus.PublishCompleted(result);

            // Get fresh recorder
            _currentRecorder?.Dispose();
            _currentRecorder = App.Current.Services.GetRequiredService<AudioRecorder>();

            // Wire audio level monitoring for visual effects
            _levelMonitor.Start();
            _currentRecorder.AudioDataAvailable += (_, e) =>
                _levelMonitor.UpdateLevel(e.PcmData, e.BytesRecorded);

            // Resolve active profile for per-preset TrailingSpace
            string? activeModeId = _modeIdOverride ?? _controlPanel.ActiveDictationModeId;
            DictationProfile? profile = activeModeId is not null
                ? _dictationModes.GetActiveProfile(activeModeId)
                : null;

            // Build DictationOptions (streaming is always raw mode)
            var options = new DictationOptions
            {
                RawMode = true,
                Language = _settings.Current.General.Language,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = profile?.TrailingSpace ?? true,
                },
            };

            // Start audio ducking
            if (_settings.Current.AudioDucking.Enabled)
            {
                _audioDucker.IsEnabled = true;
                _audioDucker.DuckLevel = _settings.Current.AudioDucking.DuckLevelPercent / 100f;
                _audioDucker.RampDownMs = _settings.Current.AudioDucking.RampDownMs;
                await _audioDucker.DuckAsync().ConfigureAwait(false);
            }

            // Start recording — AudioRecorder fires AudioDataAvailable events
            var audio = _settings.Current.Audio;
            string? deviceLabel = string.IsNullOrEmpty(audio.DeviceName) ? null : audio.DeviceName;
            _isRecording = true;
            _currentRecorder.StartRecording(
                deviceLabel: deviceLabel,
                deviceId: null,
                maxDurationSeconds: audio.MaxDurationSeconds);

            // Mute detection: sync device label, check immediately, monitor changes
            _muteDetector.UpdateDeviceLabel(deviceLabel);
            if (_muteDetector.CheckMuteState() == true)
            {
                _notifications.ShowToast(
                    _loc.GetString("Recording_MicMuted_Title"),
                    _loc.GetString("Recording_MicMuted_Message"),
                    NotificationType.Warning,
                    spokenKey: "Recording_MicMuted");
            }
            _muteDetector.MuteStateChanged += OnMuteStateChanged;
            _muteDetector.Start();

            var soundSettings = _settings.Current.Sound ?? new();
            _notifications.PlayCustomSound(soundSettings.StartSound);

            // Run pipeline (blocks until recording stops + finals drained)
            _recordingCts = new CancellationTokenSource();
            var result = await streamingPipeline.RunAsync(
                _currentRecorder, options, _recordingCts.Token);

            _isRecording = false;
            _levelMonitor.Stop();
            _muteDetector.Stop();
            _muteDetector.MuteStateChanged -= OnMuteStateChanged;
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
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Streaming Dictate pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
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

            // Pre-load local LLM into VRAM while recording (fire-and-forget)
            _ = Task.Run(() => _pipelineFactory.WarmUpLocalProvidersAsync("dictate"));

            // Record audio (waits for auto-stop event)
            var (audioFile, recordingDurationMs) = await RecordAudioAsync("Dictate", isDictate: true);
            if (audioFile == null)
            {
                Log.Warning("Dictate: No audio file produced");
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error, spokenKey: "Loading_RecordingFailed");
                return;
            }

            // Stop ducking (ramped fade-in)
            await _audioDucker.RestoreAsync().ConfigureAwait(false);

            Log.Information("Dictate: Recording complete, processing...");

            // Get active mode from settings (instead of always using modes[0])
            var modes = _dictationModes.GetAllModes();
            string? activeModeId = _modeIdOverride ?? _settings.Current.ActiveDictationModeId;

            // Fallback to first mode if ID is null or invalid
            DictationMode? activeMode = modes.FirstOrDefault(m => string.Equals(m.Id, activeModeId, StringComparison.Ordinal))
                                        ?? modes.FirstOrDefault();

            if (activeMode == null)
            {
                Log.Warning("Dictate: No dictation modes configured");
                _notifications.ShowToast("Error", _loc.GetString("Loading_NoModesConfigured"), NotificationType.Error, spokenKey: "Loading_NoModesConfigured");
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
                    TrailingSpace = profile.TrailingSpace,
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

            // Notify ControlPanel + plugins of pipeline completion
            _controlPanel.OnPipelineCompleted(this, result);
            _pipelineEventBus.PublishCompleted(result);

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
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Dictate pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task RunRefinePipelineAsync(IntPtr sourceWindow)
    {
        bool isAutoMode = _controlPanel.RefineMode == RefineMode.Auto;

        if (isAutoMode)
        {
            await RunRefineAutoAsync(sourceWindow);
        }
        else
        {
            await RunRefineVoiceAsync(sourceWindow);
        }
    }

    private async Task RunRefineAutoAsync(IntPtr sourceWindow)
    {
        try
        {
            Log.Information("Starting Refine Auto pipeline (no audio, sourceHwnd=0x{Hwnd:X})...", sourceWindow);

            // Pre-capture selection on background thread (clipboard ops + Ctrl+C simulation)
            string? preCaptured = await Task.Run(() => _textInjector.CaptureSelection(sourceWindow));
            Log.Information("Refine Auto: pre-captured {Chars} chars",
                preCaptured?.Length ?? 0);

            var soundSettings = _settings.Current.Sound ?? new();
            _notifications.PlayCustomSound(soundSettings.UtilitySound);

            string pipelineType = "refine_auto";
            UtilityProfile profile = _pipelines.GetActiveProfile(pipelineType);

            var options = new RefineOptions
            {
                SystemPrompt = profile.SystemPrompt ?? PromptDefaults.RefineAuto,
                ModelName = profile.ModelName,
                Language = _settings.Current.General.Language,
                PreCapturedText = preCaptured,
                Injection = new PipelineInjectionOptions
                {
                    TrailingSpace = _settings.Current.General.TrailingSpace,
                    AdditionalKey = string.IsNullOrEmpty(_settings.Current.General.AdditionalKey)
                        ? null
                        : _settings.Current.General.AdditionalKey,
                    SourceWindowHandle = sourceWindow,
                },
            };

            var pipeline = _pipelineFactory.CreateRefineAutoPipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await Task.Run(() => pipeline.RunAsync(null, options, _recordingCts.Token));

            _controlPanel.OnPipelineCompleted(this, result);
            _pipelineEventBus.PublishCompleted(result);
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
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Refine Auto pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
        }
        finally
        {
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task RunRefineVoiceAsync(IntPtr sourceWindow)
    {
        try
        {
            Log.Information("Starting Refine Voice pipeline (sourceHwnd=0x{Hwnd:X})...", sourceWindow);

            // Pre-load local LLM into VRAM while recording (fire-and-forget)
            _ = Task.Run(() => _pipelineFactory.WarmUpLocalProvidersAsync("refine"));

            // Record audio (waits for auto-stop event)
            var (audioFile, recordingDurationMs) = await RecordAudioAsync("Refine", isDictate: false);
            if (audioFile == null)
            {
                Log.Warning("Refine: No audio file produced");
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error, spokenKey: "Loading_RecordingFailed");
                return;
            }

            // Stop ducking (ramped fade-in)
            await _audioDucker.RestoreAsync().ConfigureAwait(false);

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
                    SourceWindowHandle = sourceWindow,
                },
                RecordingDurationMs = recordingDurationMs,
            };

            var pipeline = _pipelineFactory.CreateRefinePipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            _controlPanel.OnPipelineCompleted(this, result);
            _pipelineEventBus.PublishCompleted(result);

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
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Refine Voice pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
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
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error, spokenKey: "Loading_RecordingFailed");
                return;
            }

            // Stop ducking (ramped fade-in)
            await _audioDucker.RestoreAsync().ConfigureAwait(false);

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

            // Notify ControlPanel + plugins of pipeline completion
            var askFinalResult = ttsMs > 0 ? result with { TtsPlayedMs = ttsMs } : result;
            _controlPanel.OnPipelineCompleted(this, askFinalResult);
            _pipelineEventBus.PublishCompleted(askFinalResult);

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
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Ask pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
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
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error, spokenKey: "Loading_RecordingFailed");
                return;
            }

            // Stop ducking (ramped fade-in)
            await _audioDucker.RestoreAsync().ConfigureAwait(false);

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

            // Notify ControlPanel + plugins of pipeline completion
            var translateFinalResult = ttsMs > 0 ? result with { TtsPlayedMs = ttsMs } : result;
            _controlPanel.OnPipelineCompleted(this, translateFinalResult);
            _pipelineEventBus.PublishCompleted(translateFinalResult);

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
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Translate pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    private async Task RunNotePipelineAsync(IntPtr sourceWindow)
    {
        try
        {
            Log.Information("Starting Note pipeline (sourceHwnd=0x{Hwnd:X})...", sourceWindow);

            // Pre-capture selected text as context (same pattern as RunRefineAutoAsync)
            string? preCaptured = await Task.Run(() => _textInjector.CaptureSelection(sourceWindow));
            Log.Information("Note: pre-captured {Chars} chars", preCaptured?.Length ?? 0);

            // Record audio (waits for auto-stop event)
            var (audioFile, recordingDurationMs) = await RecordAudioAsync("Note", isDictate: false);
            if (audioFile == null)
            {
                Log.Warning("Note: No audio file produced");
                _notifications.ShowToast("Error", _loc.GetString("Loading_RecordingFailed"), NotificationType.Error, spokenKey: "Loading_RecordingFailed");
                return;
            }

            // Stop ducking (ramped fade-in)
            await _audioDucker.RestoreAsync().ConfigureAwait(false);

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
                PreCapturedContext = preCaptured,
            };

            var pipeline = _pipelineFactory.CreateNotePipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(audioFile, options, _recordingCts.Token);

            // Notify ControlPanel + plugins of pipeline completion
            _controlPanel.OnPipelineCompleted(this, result);
            _pipelineEventBus.PublishCompleted(result);

            if (result.IsSuccess)
            {
                // result.Text contains the note content that was appended
                Log.Information("Note: Saved to {FilePath}", options.NotesFilePath);
                _notifications.ShowToast(_loc.GetString("Loading_NoteSaved_Title"), _loc.GetFormatted("Loading_NoteSaved_Message", System.IO.Path.GetFileName(options.NotesFilePath)), NotificationType.Success, spokenKey: "Loading_NoteSaved");
            }
            else
            {
                Log.Warning("Note: Failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "Unknown error", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Note pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
        }
        finally
        {
            _audioDucker.Restore();
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    // ── Read Selection (SPEC_003 Phase C) ─────────────────────────────────────

    private async Task RunReadSelectionPipelineAsync(IntPtr sourceWindow)
    {
        try
        {
            Log.Information("Starting ReadSelection pipeline (sourceHwnd=0x{Hwnd:X})...", sourceWindow);

            // Check if TTS is enabled
            var ttsSettings = _settings.Current.Tts;
            if (!ttsSettings.Enabled)
            {
                Log.Information("ReadSelection: TTS is disabled");
                _notifications.ShowToast(
                    _loc.GetString("ReadSelection_Disabled_Title"),
                    _loc.GetString("ReadSelection_Disabled_Message"),
                    NotificationType.Warning,
                    spokenKey: "ReadSelection_Disabled");
                return;
            }

            // Capture selection BEFORE playing any sound (sound may steal focus).
            // Runs on background thread to avoid blocking UI.
            string? selectedText = await Task.Run(() => _textInjector.CaptureSelection(sourceWindow));

            // Play utility sound to acknowledge hotkey (after capture succeeds)
            var soundSettings = _settings.Current.Sound ?? new();
            _notifications.PlayCustomSound(soundSettings.UtilitySound);

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                Log.Warning("ReadSelection: no text selected");
                _notifications.ShowToast(
                    _loc.GetString("ReadSelection_NoSelection_Title"),
                    _loc.GetString("ReadSelection_NoSelection_Message"),
                    NotificationType.Warning,
                    spokenKey: "ReadSelection_NoSelection");
                return;
            }

            Log.Information("ReadSelection: captured {Chars} chars", selectedText.Length);

            // Start audio ducking if enabled
            bool didDuck = false;
            if (ttsSettings.DuckDuringPlayback && _settings.Current.AudioDucking.Enabled)
            {
                _audioDucker.IsEnabled = true;
                _audioDucker.DuckLevel = _settings.Current.AudioDucking.DuckLevelPercent / 100f;
                _audioDucker.RampDownMs = _settings.Current.AudioDucking.RampDownMs;
                await _audioDucker.DuckAsync().ConfigureAwait(false);
                didDuck = true;
            }

            // Create and run pipeline
            var pipeline = _pipelineFactory.CreateReadSelectionPipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;

            _recordingCts = new CancellationTokenSource();
            var result = await pipeline.RunAsync(selectedText, _recordingCts.Token);

            // Notify ControlPanel + plugins of pipeline completion
            _controlPanel.OnPipelineCompleted(this, result);
            _pipelineEventBus.PublishCompleted(result);

            if (result.IsSuccess)
            {
                Log.Information("ReadSelection: complete ({TotalMs}ms)", result.TotalMs);
            }
            else
            {
                Log.Warning("ReadSelection: failed - {Error}", result.ErrorMessage);
                _notifications.ShowToast("Error", result.ErrorMessage ?? "TTS failed", NotificationType.Error);
            }

            // Restore ducking only if WE started it (avoid stomping on TtsSpeaker's ducking)
            if (didDuck)
            {
                await _audioDucker.RestoreAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "ReadSelection pipeline failed");
                _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
            }
        }
        finally
        {
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    // ── Vision Row (CP bar integration) ──────────────────────────────────

    /// <summary>Show vision pre-capture controls in CP bar.</summary>
    private void EnterVisionMode()
    {
        Log.Information("Vision: Entering vision mode via CP bar");

        // Show dim overlay on active monitor (freeze/dim the screen)
#pragma warning disable MA0147 // Async void delegate — fire-and-forget with try/catch
        _uiDispatcher?.TryEnqueue(async () =>
        {
#pragma warning restore MA0147
            try
            {
                var monitor = ScreenCapture.GetActiveMonitorBounds();
                var monitorPng = ScreenCapture.CaptureRegion(monitor.X, monitor.Y, monitor.Width, monitor.Height);
                _dimOverlayScreenshot = monitorPng;

                var overlay = new Views.SnippingOverlayWindow();
                overlay.SetBounds(monitor.X, monitor.Y, monitor.Width, monitor.Height);
                await overlay.SetBackgroundScreenshotAsync(monitorPng).ConfigureAwait(true);
                // Default-to-Region: enable selection immediately (skip Steps 1-2 for the common case).
                // CP still shows Image/Video/Color as overrides — clicking those cancels the active selection.
                overlay.EnableSelection();
                overlay.Activate();
                _dimOverlay = overlay;
                _dimOverlayMonitorBounds = monitor;

                // Re-activate CP so it's above the dim overlay
                App.Current?.ShowMainWindow();
                App.Current?.MainWindow?.Activate();

                // Listen for overlay result — ESC cancels, region/window completes default image capture
                _ = overlay.GetResultAsync().ContinueWith(t =>
                {
                    var result = t.Result;
                    if (_dimOverlay != overlay)
                        return; // Overlay was superseded by a CP button click

                    _dimOverlay = null;

                    if (result == null)
                    {
                        // ESC — exit vision mode
                        _uiDispatcher?.TryEnqueue(() => _controlPanel.VisionExitModeCommand.Execute(null));
                        return;
                    }

                    // Default-to-Region: user drew a region (or clicked for window/pressed F for full) directly
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var capturedScreenshot = _dimOverlayScreenshot;
                            _dimOverlayScreenshot = null;

                            var basePng = capturedScreenshot ?? ScreenCapture.CaptureRegion(monitor.X, monitor.Y, monitor.Width, monitor.Height);
                            byte[]? croppedPng = result.Mode switch
                            {
                                CaptureMode.Region when result.Region is { } r =>
                                    ImageProcessor.CropRegion(basePng, r.X, r.Y, r.Width, r.Height),
                                CaptureMode.AllMonitors => ScreenCapture.CaptureFullScreen(),
                                _ => basePng,
                            };

                            _visionCapturedImageData = croppedPng;
                            await ShowPostCaptureInCpAsync(croppedPng!).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Vision: Default-to-region capture failed");
                            _uiDispatcher?.TryEnqueue(() =>
                            {
                                _controlPanel.VisionExitModeCommand.Execute(null);
                                App.Current?.ShowMainWindow();
                            });
                        }
                    });
                }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Vision: Failed to create dim overlay");
            }
        });

        _controlPanel.EnterVision(); // Snapshots state + sets CaptureType

        // Ensure CP is visible
        _uiDispatcher?.TryEnqueue(() => App.Current?.ShowMainWindow());
    }

    /// <summary>Dim overlay shown during vision wizard Steps 1-2.</summary>
    private Views.SnippingOverlayWindow? _dimOverlay;
    private byte[]? _dimOverlayScreenshot;
    private (int X, int Y, int Width, int Height) _dimOverlayMonitorBounds;

    /// <summary>User clicked a Step 1 override button — cancel the default-to-region overlay without exiting vision mode.</summary>
    private void OnVisionDefaultOverridden(object? sender, EventArgs e)
    {
        _uiDispatcher?.TryEnqueue(() =>
        {
            var overlay = _dimOverlay;
            if (overlay == null) return;
            _dimOverlay = null; // Null first so the GetResultAsync continuation exits early
            overlay.DismissDim();
            Log.Information("Vision: Default-to-region cancelled — user chose override");
        });
    }

    /// <summary>Close the dim overlay if open.</summary>
    private void DismissDimOverlay()
    {
        _uiDispatcher?.TryEnqueue(() =>
        {
            _dimOverlay?.DismissDim();
            _dimOverlay = null;
            _dimOverlayScreenshot = null;
        });
    }

    /// <summary>Captured image data held between capture and action selection.</summary>
    private byte[]? _visionCapturedImageData;
    private string? _visionCapturedImagePath;

    /// <summary>Captured video path + size held between recording and action selection.</summary>
    private string? _capturedVideoPath;
    private long _capturedVideoSize;

    private void OnVisionCaptureRequested(object? sender, VisionCaptureType captureType)
    {
        // Dispatch off UI thread to avoid blocking button click
        _ = Task.Run(() => HandleVisionCaptureAsync(captureType));
    }

    private void OnVisionActionChosen(object? sender, VisionActionChosenEventArgs args)
    {
        // Dispatch off UI thread immediately to avoid blocking
        _ = Task.Run(() => HandleVisionActionAsync(args));
    }

    private CancellationTokenSource? _videoRecordingCts;

    private void OnRecordingStopRequested(object? sender, EventArgs e)
    {
        _videoRecordingCts?.Cancel();
    }

    private void OnRecordingPauseToggleRequested(object? sender, EventArgs e)
    {
        // [PHASE_5] Wire to VideoCapture pause/resume when migrating video controls
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Pipeline orchestrator")]
    private async Task HandleVisionCaptureAsync(VisionCaptureType captureType)
    {
        try
        {
            Log.Information("Vision: Capture requested — {Type}", captureType);

            if (captureType == VisionCaptureType.ColorPick)
            {
                // Color picker uses the dim overlay screenshot, then dismisses dim
                var colorCapture = _dimOverlayScreenshot;
                DismissDimOverlay();
                _uiDispatcher?.TryEnqueue(() => _controlPanel.VisionExitModeCommand.Execute(null));
                if (colorCapture != null)
                    await HandleVisionColorPickAsync(colorCapture).ConfigureAwait(false);
                return;
            }

            if (captureType == VisionCaptureType.VideoRegion || captureType == VisionCaptureType.VideoFull)
            {
                DismissDimOverlay(); // Dismiss dim before recording (or it appears in video)
                await HandleVideoCaptureViaCpAsync(captureType).ConfigureAwait(false);
                return;
            }

            // Screenshot Full: use the already-captured dim overlay screenshot
            if (captureType == VisionCaptureType.ScreenshotFull)
            {
                var monitorPng = _dimOverlayScreenshot;
                DismissDimOverlay();
                if (monitorPng != null)
                {
                    _visionCapturedImageData = monitorPng;
                    await ShowPostCaptureInCpAsync(monitorPng).ConfigureAwait(false);
                }
                return;
            }

            // Screenshot Region: enable selection on existing dim overlay
            Log.Information("Vision: Enabling selection on dim overlay...");
            var activeMonitor = _dimOverlayMonitorBounds.Width > 0
                ? _dimOverlayMonitorBounds
                : ScreenCapture.GetActiveMonitorBounds();
            byte[]? fullPng = ScreenCapture.CaptureFullScreen();

            // Hide CP, enable selection on existing dim overlay
            _uiDispatcher?.TryEnqueue(() => App.Current?.HideMainWindow());

            Views.SnippingResult? snippingResult = null;
            var overlayTcs = new TaskCompletionSource<Views.SnippingResult?>();

#pragma warning disable MA0147 // Async void delegate — exceptions routed via TCS
            _uiDispatcher!.TryEnqueue(async () =>
            {
#pragma warning restore MA0147
                try
                {
                    var overlay = _dimOverlay;
                    if (overlay != null)
                    {
                        overlay.EnableSelection();
                        overlay.Activate();
                        var result = await overlay.GetResultAsync().ConfigureAwait(true);
                        _dimOverlay = null; // Overlay closes itself after result
                        overlayTcs.TrySetResult(result);
                    }
                    else
                    {
                        // No dim overlay — fallback: create fresh overlay
                        var monitorPng = ScreenCapture.CaptureRegion(activeMonitor.X, activeMonitor.Y, activeMonitor.Width, activeMonitor.Height);
                        var freshOverlay = new Views.SnippingOverlayWindow();
                        freshOverlay.SetBounds(activeMonitor.X, activeMonitor.Y, activeMonitor.Width, activeMonitor.Height);
                        await freshOverlay.SetBackgroundScreenshotAsync(monitorPng).ConfigureAwait(true);
                        freshOverlay.Activate();
                        overlayTcs.TrySetResult(await freshOverlay.GetResultAsync().ConfigureAwait(true));
                    }
                }
                catch (Exception ex)
                {
                    overlayTcs.TrySetException(ex);
                }
            });
            snippingResult = await overlayTcs.Task.ConfigureAwait(false);
            var capturedScreenshot = _dimOverlayScreenshot; // Save before nulling
            _dimOverlayScreenshot = null;
            Log.Information("Vision: Snipping result — {Mode}", snippingResult?.Mode);

            if (snippingResult == null)
            {
                // Cancelled — show CP again, return to capture type
                _uiDispatcher?.TryEnqueue(() =>
                {
                    App.Current?.ShowMainWindow();
                    _controlPanel.VisionPhase = VisionWizardStep.CaptureType;
                });
                return;
            }

            // Crop the captured image based on selection
            var basePng = capturedScreenshot ?? ScreenCapture.CaptureRegion(activeMonitor.X, activeMonitor.Y, activeMonitor.Width, activeMonitor.Height);
            byte[]? croppedPng = snippingResult.Mode switch
            {
                CaptureMode.Region when snippingResult.Region is { } r =>
                    ImageProcessor.CropRegion(basePng, r.X, r.Y, r.Width, r.Height),
                CaptureMode.AllMonitors => fullPng,
                _ => basePng,
            };

            _visionCapturedImageData = croppedPng;

            // Save original + show post-capture UI
            await ShowPostCaptureInCpAsync(croppedPng!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Vision: Capture failed");
            _uiDispatcher?.TryEnqueue(() =>
            {
                _controlPanel.VisionExitModeCommand.Execute(null);
                App.Current?.ShowMainWindow();
            });
        }
    }

    private DispatcherQueueTimer? _recordingTimer;
    private Views.RecordingBorderOverlay? _recordingBorderOverlay;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Video recording orchestrator")]
    private async Task HandleVideoCaptureViaCpAsync(VisionCaptureType captureType)
    {
        try
        {
            bool isFullScreen = captureType == VisionCaptureType.VideoFull;

            // Use the frozen monitor bounds from Step 1 (not current active monitor)
            var monitorBounds = _dimOverlayMonitorBounds.Width > 0
                ? _dimOverlayMonitorBounds
                : ScreenCapture.GetActiveMonitorBounds();
            int left = monitorBounds.X, top = monitorBounds.Y;
            int width = monitorBounds.Width, height = monitorBounds.Height;

            // For region: show snipping overlay first
            if (!isFullScreen)
            {
                _uiDispatcher?.TryEnqueue(() => App.Current?.HideMainWindow());
                await Task.Delay(100).ConfigureAwait(false);

                var monitorPng = ScreenCapture.CaptureRegion(left, top, width, height);
                Views.SnippingResult? snippingResult = null;
                var overlayTcs = new TaskCompletionSource<Views.SnippingResult?>();
#pragma warning disable MA0147
                _uiDispatcher!.TryEnqueue(async () =>
                {
                    try
                    {
                        var overlay = new Views.SnippingOverlayWindow();
                        overlay.SetBounds(left, top, width, height);
                        await overlay.SetBackgroundScreenshotAsync(monitorPng).ConfigureAwait(true);
                        overlay.Activate();
                        overlayTcs.TrySetResult(await overlay.GetResultAsync().ConfigureAwait(true));
                    }
                    catch (Exception ex) { overlayTcs.TrySetException(ex); }
                });
#pragma warning restore MA0147
                snippingResult = await overlayTcs.Task.ConfigureAwait(false);

                if (snippingResult == null)
                {
                    _uiDispatcher?.TryEnqueue(() =>
                    {
                        App.Current?.ShowMainWindow();
                        _controlPanel.VisionPhase = VisionWizardStep.CaptureType;
                    });
                    return;
                }

                if (snippingResult.Mode == CaptureMode.Region && snippingResult.Region is { } r)
                {
                    left = monitorBounds.X + r.X;
                    top = monitorBounds.Y + r.Y;
                    width = r.Width;
                    height = r.Height;
                }
            }

            // Show recording border (pure Win32 layered window, excluded from capture)
            if (!isFullScreen)
            {
                _recordingBorderOverlay = new Views.RecordingBorderOverlay(
                    left, top, width, height,
                    monitorBounds.X, monitorBounds.Y, monitorBounds.Width, monitorBounds.Height);
                await _recordingBorderOverlay.ShowAsync().ConfigureAwait(false);
            }

            // Prepare output path
            string visionDir = ResolveVisionFolder();
            Directory.CreateDirectory(visionDir);
            string outputPath = Path.Combine(visionDir,
                $"video_{DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)}.mp4");

            // Switch CP to recording phase
            var stopTcs = new TaskCompletionSource<bool>();
            _videoRecordingCts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var recordingStart = DateTime.UtcNow;

            // Start recording timer on UI thread
            _uiDispatcher?.TryEnqueue(() =>
            {
                _controlPanel.VisionPhase = VisionWizardStep.Recording;
                _controlPanel.RecordingTimerText = "00:00";
                _controlPanel.IsRecordingPaused = false;
                _controlPanel.StatusText = "WORKING";
                _controlPanel.CurrentState = PipelineState.Processing; // Prevents audio monitor from overwriting to "READY"

                if (isFullScreen)
                {
                    // Full screen: hide CP so it doesn't appear in recording
                    App.Current?.HideMainWindow();

                    // Poll ESC key to stop recording (no WndProc needed)
                    _ = Task.Run(async () =>
                    {
                        while (!_videoRecordingCts?.IsCancellationRequested ?? false)
                        {
                            if ((NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0) // VK_ESCAPE
                            {
                                Log.Information("Video: ESC pressed — stopping full-screen recording");
                                _videoRecordingCts?.Cancel();
                                break;
                            }
                            await Task.Delay(100).ConfigureAwait(false);
                        }
                    });
                }
                else
                {
                    // Region: keep CP visible for controls
                    App.Current?.ShowMainWindow();
                }

                // Timer for elapsed display
                _recordingTimer = _uiDispatcher!.CreateTimer();
                _recordingTimer.Interval = TimeSpan.FromMilliseconds(500);
                _recordingTimer.Tick += (s, e) =>
                {
                    var elapsed = DateTime.UtcNow - recordingStart;
                    _controlPanel.RecordingTimerText = elapsed.ToString(@"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);
                };
                _recordingTimer.Start();
            });

            // Wire stop event
            void OnStop(object? s, EventArgs e) => stopTcs.TrySetResult(true);
            _controlPanel.RecordingStopRequested += OnStop;

            // Start capture — read recording settings from AppSettings
            var vs = _settings.Current.Vision;
            var (bitrate, fps) = vs.VideoQuality switch
            {
                "low" => (1500, 24),
                "high" => (8000, 60),
                _ => (5000, 30),
            };
            var options = new VideoRecordingOptions
            {
                EnableWebcam = vs.EnableWebcam,
                WebcamBubbleSize = vs.WebcamSize,
                WebcamPosition = vs.WebcamPosition,
                BitrateKbps = bitrate,
                FrameRateHz = fps,
                EnableMicAudio = vs.EnableMicAudio,
                EnableSystemAudio = vs.EnableSystemAudio,
                MicDeviceName = string.IsNullOrEmpty(vs.MicDeviceName) ? null : vs.MicDeviceName,
                SystemAudioDeviceName = string.IsNullOrEmpty(vs.SystemAudioDeviceName) ? null : vs.SystemAudioDeviceName,
            };
            using var capture = new VideoCapture();

            _ = stopTcs.Task.ContinueWith(_ => capture.Stop(), TaskScheduler.Default);
            _ = _videoRecordingCts.Token.Register(() => stopTcs.TrySetResult(true));

            try
            {
                if (!isFullScreen)
                    _notifications.ShowToast("Video", "Recording started...", NotificationType.Info, suppressTts: true);
                // Resolve the correct monitor for multi-monitor recording
                var monitorDevice = ScreenCapture.GetMonitorDeviceName(left + width / 2, top + height / 2);
                await capture.RecordAsync(left, top, width, height, outputPath, options, _videoRecordingCts.Token,
                    monitorDevice, monitorBounds.X, monitorBounds.Y).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Log.Information("Video: recording stopped by user or timeout");
            }
            finally
            {
                _controlPanel.RecordingStopRequested -= OnStop;
                _recordingBorderOverlay?.Dispose();
                _recordingBorderOverlay = null;
                _uiDispatcher?.TryEnqueue(() =>
                {
                    _recordingTimer?.Stop();
                    _recordingTimer = null;
                    _controlPanel.StatusText = "READY";
                    _controlPanel.CurrentState = PipelineState.Idle;
                });
                _videoRecordingCts?.Dispose();
                _videoRecordingCts = null;
            }

            // Post-recording
            long fileSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
            Log.Information("Video: saved to {Path} ({Size} bytes)", outputPath, fileSize);

            if (fileSize == 0)
            {
                // 0-byte cleanup
                try { File.Delete(outputPath); } catch { /* ignore */ }
                _notifications.ShowToast("Video", "Recording failed (empty file)", NotificationType.Error, suppressTts: true);
                _uiDispatcher?.TryEnqueue(() =>
                {
                    _controlPanel.VisionExitModeCommand.Execute(null);
                    App.Current?.ShowMainWindow();
                });
                return;
            }

            // Transition to PostCapture in wizard (video-specific buttons)
            _capturedVideoPath = outputPath;
            _capturedVideoSize = fileSize;
            _uiDispatcher?.TryEnqueue(() =>
            {
                _controlPanel.IsVideoPostCapture = true;
                _controlPanel.VisionPhase = VisionWizardStep.PostCapture;
                App.Current?.ShowMainWindow();
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Video: capture via CP failed");
            _recordingBorderOverlay?.Dispose();
            _recordingBorderOverlay = null;
            _uiDispatcher?.TryEnqueue(() =>
            {
                _controlPanel.VisionExitModeCommand.Execute(null);
                _recordingTimer?.Stop();
                _recordingTimer = null;
                App.Current?.ShowMainWindow();
            });
            _notifications.ShowToast("Video Error", ex.Message, NotificationType.Error, suppressTts: true);
        }
    }

    private async Task ShowPostCaptureInCpAsync(byte[] imageData)
    {
        // Save the raw capture
        var visionDir = ResolveVisionFolder();
        Directory.CreateDirectory(visionDir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        _visionCapturedImagePath = Path.Combine(visionDir, $"vision_{timestamp}.png");
        await File.WriteAllBytesAsync(_visionCapturedImagePath, imageData).ConfigureAwait(false);
        Log.Information("Vision: Saved capture to {Path}", _visionCapturedImagePath);

        // Create thumbnail and show post-capture phase
#pragma warning disable MA0147 // Async void delegate — fire-and-forget UI update with try/catch
        _uiDispatcher?.TryEnqueue(async () =>
        {
            try
            {
                // Create BitmapImage from byte array for thumbnail
                var bitmapImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                using var stream = new MemoryStream(imageData);
                var ras = stream.AsRandomAccessStream();
                await bitmapImage.SetSourceAsync(ras);
                _controlPanel.VisionThumbnail = bitmapImage;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Vision: Failed to create thumbnail");
            }

            _controlPanel.VisionPhase = VisionWizardStep.PostCapture;

            // Show CP window (don't force expand — vision row is visible regardless)
            App.Current?.ShowMainWindow();
        });
#pragma warning restore MA0147
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Pipeline orchestrator")]
    private async Task HandleVisionActionAsync(VisionActionChosenEventArgs args)
    {
        try
        {
            Log.Information("Vision: Action chosen — {Action}, Query={Query}, Local={Local}, SkipAi={SkipAi}",
                args.Action, args.UserQuery, args.UseLocal, args.SkipAi);

            // Video post-capture: route to video handlers
            if (_controlPanel.IsVideoPostCapture && _capturedVideoPath != null)
            {
                await HandleVideoActionFromWizardAsync(args).ConfigureAwait(false);
                return;
            }

            var imageData = _visionCapturedImageData;
            if (imageData == null)
            {
                Log.Warning("Vision: No captured image data for action");
                _uiDispatcher?.TryEnqueue(() => _controlPanel.VisionExitModeCommand.Execute(null));
                return;
            }

            // Build a VisionActionResult to reuse the existing dispatch logic
            var actionResult = new DiktaMe.Core.Vision.VisionActionResult(
                args.Action, args.UserQuery, args.UseLocal, args.SkipAi);

            // Resolve provider + model (same logic as old RunVisionPipelineAsync)
            var visionSettings = _settings.Current.Vision;
            string visionProvider;
            string visionModelId;
            if (args.UseLocal)
            {
                visionProvider = "ollama";
                visionModelId = visionSettings.LocalVisionModelId;
            }
            else
            {
                visionProvider = visionSettings.CloudVisionProvider;
                visionModelId = visionSettings.CloudVisionModelId;
            }

            // Prepare image for API (returns (byte[] Data, string MimeType) tuple)
            var prepared = ImageProcessor.PrepareForApi(imageData);
            var apiImage = prepared.Data;
            var mimeType = prepared.MimeType;
            var savedPath = _visionCapturedImagePath;

            Log.Information("Vision: Dispatching action {Action} to {Provider}/{Model}...", args.Action, visionProvider, visionModelId);

            // Show "Thinking..." for AI actions
            bool isAiAction = args.Action is not (DiktaMe.Core.Vision.VisionAction.Save or DiktaMe.Core.Vision.VisionAction.Edit);
            if (isAiAction && !args.SkipAi)
            {
                _uiDispatcher?.TryEnqueue(() => _controlPanel.IsVisionProcessing = true);
            }

            try
            {
                switch (args.Action)
                {
                    case DiktaMe.Core.Vision.VisionAction.Save:
                        await HandleVisionSaveAsync(imageData, savedPath).ConfigureAwait(false);
                        break;

                    case DiktaMe.Core.Vision.VisionAction.Clipboard:
                        if (args.SkipAi)
                        {
                            CopyImageToClipboard(imageData);
                            _notifications.ShowToast("Vision", "Image copied to clipboard", suppressTts: true);
                        }
                        else
                        {
                            await HandleVisionClipboardAsync(apiImage, mimeType, visionProvider, visionModelId, visionSettings, actionResult).ConfigureAwait(false);
                        }
                        break;

                    case DiktaMe.Core.Vision.VisionAction.Chat:
                        await HandleVisionChatAsync(imageData, mimeType, visionProvider, visionModelId, actionResult).ConfigureAwait(false);
                        break;

                    case DiktaMe.Core.Vision.VisionAction.Note:
                        if (args.SkipAi)
                        {
                            // Save note with image only, no AI description
                            CopyImageToClipboard(imageData);
                            _notifications.ShowToast("Vision", "Image saved (no AI)", suppressTts: true);
                        }
                        else
                        {
                            await HandleVisionNoteAsync(imageData, mimeType, savedPath ?? "", visionProvider, visionModelId, visionSettings, actionResult).ConfigureAwait(false);
                        }
                        break;

                    case DiktaMe.Core.Vision.VisionAction.Ocr:
                        await HandleVisionOcrAsync(imageData, mimeType, visionProvider, visionModelId, visionSettings, actionResult).ConfigureAwait(false);
                        break;

                    case DiktaMe.Core.Vision.VisionAction.Table:
                        await HandleVisionTableAsync(imageData, mimeType, visionProvider, visionModelId, visionSettings, actionResult).ConfigureAwait(false);
                        break;

                    case DiktaMe.Core.Vision.VisionAction.Edit:
                        Log.Information("Vision: Opening annotation editor");
                        var editTcs = new TaskCompletionSource<Views.AnnotationResult?>();
                        _uiDispatcher?.TryEnqueue(() => _ = OpenAnnotationEditorAsync(editTcs, imageData));
                        var editResult = await editTcs.Task.ConfigureAwait(false);
                        if (editResult?.ImageData != null)
                        {
                            _visionCapturedImageData = editResult.ImageData;
                            // Save annotated version
                            var annotatedPath = _visionCapturedImagePath?.Replace(".png", "_annotated.png", StringComparison.OrdinalIgnoreCase);
                            if (annotatedPath != null)
                                await File.WriteAllBytesAsync(annotatedPath, editResult.ImageData).ConfigureAwait(false);
                        }
                        // Return to PostCapture with updated image (don't exit wizard)
                        _uiDispatcher?.TryEnqueue(() => _controlPanel.VisionPhase = VisionWizardStep.PostCapture);
                        return; // Skip the exit-vision at the bottom

                    default:
                        Log.Warning("Vision: Unhandled action {Action}", args.Action);
                        break;
                }
            }
            finally
            {
                _uiDispatcher?.TryEnqueue(() => _controlPanel.IsVisionProcessing = false);
            }

            // Done — exit vision mode (must dispatch to UI thread for property change notifications)
            Log.Information("Vision: Action {Action} completed, exiting vision mode", args.Action);
            _visionCapturedImageData = null;
            _visionCapturedImagePath = null;
            _uiDispatcher?.TryEnqueue(() => _controlPanel.VisionExitModeCommand.Execute(null));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Vision: Action {Action} failed", args.Action);
            _uiDispatcher?.TryEnqueue(() => _controlPanel.VisionExitModeCommand.Execute(null));
        }
    }

    private async Task HandleVisionSaveAsync(byte[] imageData, string? savedPath)
    {
        var saveTcs = new TaskCompletionSource<string?>();
#pragma warning disable MA0147 // Async void delegate — exceptions routed via TCS
        _uiDispatcher?.TryEnqueue(async () =>
        {
#pragma warning restore MA0147
            try
            {
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                // WinUI 3 requires InitializeWithWindow
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current?.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                picker.SuggestedFileName = $"vision_{DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)}";
                picker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" });

                var file = await picker.PickSaveFileAsync();
                saveTcs.TrySetResult(file?.Path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Vision: FileSavePicker failed");
                saveTcs.TrySetResult(null);
            }
        });

        var chosenPath = await saveTcs.Task.ConfigureAwait(false);
        if (!string.IsNullOrEmpty(chosenPath))
        {
            await File.WriteAllBytesAsync(chosenPath, imageData).ConfigureAwait(false);
            _notifications.ShowToast("Vision", $"Saved to {Path.GetFileName(chosenPath)}", spokenKey: "Vision_ImageSaved");
            Log.Information("Vision: Saved to user-chosen path {Path}", chosenPath);
        }
        else if (savedPath != null)
        {
            // User cancelled picker — file is still in vision folder
            _notifications.ShowToast("Vision", $"Auto-saved to {Path.GetFileName(savedPath)}", spokenKey: "Vision_ImageSaved");
        }
    }

    /// <summary>Handle video post-capture actions from the wizard (Describe/Document/BugReport/Save).</summary>
    private async Task HandleVideoActionFromWizardAsync(VisionActionChosenEventArgs args)
    {
        var videoPath = _capturedVideoPath!;
        var fileSize = _capturedVideoSize;

        try
        {
            // Save action — just notify
            if (args.Action == DiktaMe.Core.Vision.VisionAction.Save)
            {
                _notifications.ShowToast("Video", $"Saved ({fileSize / 1024}KB) → {Path.GetFileName(videoPath)}", NotificationType.Success, suppressTts: true);
                _uiDispatcher?.TryEnqueue(() => _controlPanel.VisionExitModeCommand.Execute(null));
                return;
            }

            // Map vision actions to video prompts (from settings)
            var vs = _settings.Current.Vision;
            string defaultQuery = args.UserQuery ?? args.Action switch
            {
                DiktaMe.Core.Vision.VisionAction.Clipboard => vs.VideoDescribePrompt,
                DiktaMe.Core.Vision.VisionAction.Chat => vs.VideoDocumentPrompt,
                DiktaMe.Core.Vision.VisionAction.Note => vs.VideoBugReportPrompt,
                _ => vs.VideoDescribePrompt,
            };

            _uiDispatcher?.TryEnqueue(() => _controlPanel.IsVisionProcessing = true);
            _notifications.ShowToast("Video AI", "Analyzing video with Gemini...", NotificationType.Info, suppressTts: true);

            byte[] videoData = await File.ReadAllBytesAsync(videoPath).ConfigureAwait(false);
            var appSettings = _settings.Current;
            string provider = appSettings.Vision.CloudVisionProvider ?? "gemini";
            string model = appSettings.Vision.CloudVisionModelId ?? "gemini-2.5-flash";
            var pipeline = _pipelineFactory.CreateVisionPipeline(provider, model);

            var visionOptions = new DiktaMe.Core.Vision.VisionOptions
            {
                DefaultQuery = defaultQuery,
                SystemPrompt = vs.VideoSystemPrompt,
            };

            var result = await pipeline.RunAsync(videoData, "video/mp4", audioFilePath: null, visionOptions, CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Text))
            {
                // Always copy to clipboard
                CopyTextToClipboard(result.Text);

                // Optionally also inject at cursor
                if (_settings.Current.Vision.VideoAiInjectAtCursor)
                {
                    _textInjector.InjectText(result.Text, trailingSpace: false);
                }

                Log.Information("Video AI: {Chars} chars → clipboard{Inject}", result.Text.Length,
                    _settings.Current.Vision.VideoAiInjectAtCursor ? " + cursor" : "");
                _notifications.ShowToast("Video AI", $"Analysis complete ({result.Text.Length} chars)", NotificationType.Success, suppressTts: true);
            }
            else
            {
                _notifications.ShowToast("Video AI", result.ErrorMessage ?? "No result", NotificationType.Warning, suppressTts: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Video AI: wizard action failed");
            _notifications.ShowToast("Video AI", ex.Message, NotificationType.Error, suppressTts: true);
        }
        finally
        {
            _capturedVideoPath = null;
            _capturedVideoSize = 0;
            _uiDispatcher?.TryEnqueue(() =>
            {
                _controlPanel.IsVisionProcessing = false;
                _controlPanel.VisionExitModeCommand.Execute(null);
            });
        }
    }

    // ── Vision Pipeline (SPEC_015-0C) — Legacy flow ──────────────────────

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Pipeline orchestrator")]
    private async Task RunVisionPipelineAsync()
    {
        try
        {
            Log.Information("Starting Vision pipeline...");
            _annotationContext = null; // Reset from previous run

            // Step 1: Capture the active monitor BEFORE showing overlay
            var bounds = DiktaMe.Core.Vision.ScreenCapture.GetActiveMonitorBounds();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                _notifications.ShowToast("Vision", "No active monitor found", NotificationType.Error, suppressTts: true);
                return;
            }

            Log.Debug("Vision: capturing active monitor ({W}x{H} at {X},{Y})",
                bounds.Width, bounds.Height, bounds.X, bounds.Y);

            // Pre-capture both active monitor AND full virtual screen BEFORE showing overlay.
            // This avoids the overlay (black dim) appearing in the captured image.
            byte[] monitorPng = null!;
            byte[] allMonitorsPng = null!;
            await Task.Run(() =>
            {
                monitorPng = DiktaMe.Core.Vision.ScreenCapture.CaptureRegion(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                allMonitorsPng = DiktaMe.Core.Vision.ScreenCapture.CaptureFullScreen();
            }).ConfigureAwait(true);
            Log.Debug("Vision: active monitor captured ({Size} bytes), all monitors ({AllSize} bytes)",
                monitorPng.Length, allMonitorsPng.Length);

            if (monitorPng.Length == 0)
            {
                _notifications.ShowToast("Vision", "Screen capture returned empty image", NotificationType.Error, suppressTts: true);
                return;
            }

            // Step 2: Show snipping overlay sized to the active window
            Log.Debug("Vision: creating overlay for bounds ({X},{Y} {W}x{H})", bounds.X, bounds.Y, bounds.Width, bounds.Height);
            var overlay = new Views.SnippingOverlayWindow();
            overlay.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            Log.Debug("Vision: loading background screenshot ({Size} bytes)", monitorPng.Length);
            await overlay.SetBackgroundScreenshotAsync(monitorPng).ConfigureAwait(true);
            Log.Debug("Vision: activating overlay");
            overlay.Activate();
            var snippingResult = await overlay.GetResultAsync().ConfigureAwait(true);

            if (snippingResult is null)
            {
                Log.Information("Vision: cancelled by user");
                return;
            }

            // Step 3: Process capture — use the window image directly or extract sub-region
            Log.Debug("Vision: processing capture (mode={Mode})", snippingResult.Mode);
            var visionSettings = _settings.Current.Vision;
            var captureSw = System.Diagnostics.Stopwatch.StartNew();

            var (imageData, mimeType) = await Task.Run(() =>
            {
                byte[] screenshot;
                if (snippingResult.Mode == DiktaMe.Core.Vision.CaptureMode.Region
                    && snippingResult.Region is { } region)
                {
                    // Crop from the pre-captured monitor image to avoid timing race
                    // (overlay may still be visible if we re-capture from screen)
                    screenshot = DiktaMe.Core.Vision.ImageProcessor.CropRegion(
                        monitorPng, region.X, region.Y, region.Width, region.Height);
                }
                else if (snippingResult.Mode == DiktaMe.Core.Vision.CaptureMode.AllMonitors)
                {
                    // Use pre-captured full virtual screen (captured before overlay was shown)
                    screenshot = allMonitorsPng;
                    Log.Debug("Vision: using pre-captured all monitors ({Size} bytes)", screenshot.Length);
                }
                else
                {
                    // FullScreen (active monitor) or ActiveWindow — use pre-captured monitor PNG
                    screenshot = monitorPng;
                }

                Log.Debug("Vision: captured {Size} bytes, preparing for API", screenshot.Length);
                return DiktaMe.Core.Vision.ImageProcessor.PrepareForApi(
                    screenshot, visionSettings.MaxImageDimensionPx);
            }).ConfigureAwait(true);

            captureSw.Stop();
            long captureMs = captureSw.ElapsedMilliseconds;

            // Compute captured image dimensions for telemetry
            int imgWidth, imgHeight;
            if (snippingResult.Mode == DiktaMe.Core.Vision.CaptureMode.Region
                && snippingResult.Region is { } telemetryRegion)
            {
                imgWidth = telemetryRegion.Width;
                imgHeight = telemetryRegion.Height;
            }
            else if (snippingResult.Mode == DiktaMe.Core.Vision.CaptureMode.AllMonitors)
            {
                var virtualScreen = DiktaMe.Core.Vision.ScreenCapture.GetVirtualScreenBounds();
                imgWidth = virtualScreen.Width;
                imgHeight = virtualScreen.Height;
            }
            else
            {
                imgWidth = bounds.Width;
                imgHeight = bounds.Height;
            }

            if (imageData.Length == 0)
            {
                _notifications.ShowToast("Vision", "Screen capture failed", NotificationType.Error, suppressTts: true);
                return;
            }

            // Save screenshot for debugging / note image links
            string visionDir = ResolveVisionFolder();
            Directory.CreateDirectory(visionDir);
            string ext = string.Equals(mimeType, "image/jpeg", StringComparison.Ordinal) ? "jpg" : "png";
            string savedPath = Path.Combine(visionDir,
                $"vision_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
            await File.WriteAllBytesAsync(savedPath, imageData).ConfigureAwait(true);
            Log.Information("Vision: image saved to {Path} ({MimeType}, {Size} bytes)",
                savedPath, mimeType, imageData.Length);

            // Step 4: Show action modal — user picks Clipboard / Chat / Note + Local/Cloud
            // Must create Window on UI thread — DispatcherQueue ensures this even if
            // we drifted off the UI thread after ConfigureAwait calls above.
            var actionTcs = new TaskCompletionSource<DiktaMe.Core.Vision.VisionActionResult?>();
            _uiDispatcher!.TryEnqueue(() =>
            {
                _ = ShowVisionActionWindowAsync(actionTcs, imageData, bounds);
            });
            var actionResult = await actionTcs.Task.ConfigureAwait(false);

            if (actionResult is null)
            {
                Log.Information("Vision: action cancelled by user");
                return;
            }

            Log.Information("Vision: action={Action}, useLocal={Local}, query={Query}",
                actionResult.Action, actionResult.UseLocal, actionResult.UserQuery ?? "(default)");

            // Set vision telemetry for history logging
            _pendingVisionTelemetry = (
                CaptureMode: snippingResult.Mode.ToString(),
                ActionType: actionResult.Action.ToString(),
                ImageWidth: imgWidth,
                ImageHeight: imgHeight,
                CaptureMs: captureMs
            );

            // Resolve provider from modal toggle
            string visionProvider = actionResult.UseLocal
                ? "ollama"
                : (visionSettings.CloudVisionProvider ?? "gemini");
            string visionModelId = actionResult.UseLocal
                ? (visionSettings.LocalVisionModelId ?? "minicpm-v")
                : (visionSettings.CloudVisionModelId ?? "gemini-2.5-flash");

            // "None" mode — skip all AI, just copy raw image to clipboard.
            // Save and Color actions handle their own flow, so let them through.
            if (actionResult.SkipAi
                && actionResult.Action != DiktaMe.Core.Vision.VisionAction.Save
                && actionResult.Action != DiktaMe.Core.Vision.VisionAction.Color
                && actionResult.Action != DiktaMe.Core.Vision.VisionAction.Record
                && actionResult.Action != DiktaMe.Core.Vision.VisionAction.Edit)
            {
                CopyImageToClipboard(imageData);
                _notifications.ShowToast("Vision", "Image copied to clipboard (no AI)",
                    NotificationType.Success, suppressTts: true);
                await LogVisionOnlyAsync("vision", captureMs, snippingResult.Mode, actionResult.Action, imgWidth, imgHeight).ConfigureAwait(false);
                return;
            }

            // Table requires a capable cloud model — force cloud, warn user
            if (actionResult.Action == DiktaMe.Core.Vision.VisionAction.Table && actionResult.UseLocal)
            {
                string cloudProvider = visionSettings.CloudVisionProvider ?? "gemini";
                string cloudModel = visionSettings.CloudVisionModelId ?? "gemini-2.5-flash";
                if (string.IsNullOrWhiteSpace(cloudProvider))
                {
                    _notifications.ShowToast("Vision", "Table extraction requires a cloud model. Configure one in Settings > AI Engine > Vision.",
                        NotificationType.Error, suppressTts: true);
                    return;
                }

                Log.Information("Vision: Table forced to cloud ({Provider}/{Model}) — local models produce unreliable output",
                    cloudProvider, cloudModel);
                _notifications.ShowToast("Vision", "Table uses cloud model for accuracy", NotificationType.Info, suppressTts: true);
                visionProvider = cloudProvider;
                visionModelId = cloudModel;
            }

            // Step 5: Branch on action
            switch (actionResult.Action)
            {
                case DiktaMe.Core.Vision.VisionAction.Save:
                    // No AI — copy image to clipboard + offer FileSavePicker for custom destination
                    CopyImageToClipboard(imageData);
                    await SaveVisionWithPickerAsync(savedPath, imageData).ConfigureAwait(false);
                    await LogVisionOnlyAsync("vision", captureMs, snippingResult.Mode, actionResult.Action, imgWidth, imgHeight).ConfigureAwait(false);
                    return;

                case DiktaMe.Core.Vision.VisionAction.Chat:
                    await HandleVisionChatAsync(imageData, mimeType, visionProvider, visionModelId, actionResult).ConfigureAwait(false);
                    return;

                case DiktaMe.Core.Vision.VisionAction.Note:
                    await HandleVisionNoteAsync(imageData, mimeType, savedPath, visionProvider, visionModelId, visionSettings, actionResult).ConfigureAwait(false);
                    return;

                case DiktaMe.Core.Vision.VisionAction.Ocr:
                    await HandleVisionOcrAsync(imageData, mimeType, visionProvider, visionModelId, visionSettings, actionResult).ConfigureAwait(false);
                    return;

                case DiktaMe.Core.Vision.VisionAction.Table:
                    await HandleVisionTableAsync(imageData, mimeType, visionProvider, visionModelId, visionSettings, actionResult).ConfigureAwait(false);
                    return;

                case DiktaMe.Core.Vision.VisionAction.Color:
                    await HandleVisionColorPickAsync(imageData).ConfigureAwait(false);
                    await LogVisionOnlyAsync("color_pick", captureMs, snippingResult.Mode, actionResult.Action, imgWidth, imgHeight).ConfigureAwait(false);
                    return;

                case DiktaMe.Core.Vision.VisionAction.Record:
                    await HandleVideoRecordAsync(bounds, snippingResult).ConfigureAwait(false);
                    return;

                case DiktaMe.Core.Vision.VisionAction.Edit:
                    var editResult = await HandleAnnotationEditAsync(imageData).ConfigureAwait(false);
                    if (editResult != null)
                    {
                        imageData = editResult.ImageData;
                        _annotationContext = editResult.AnnotationContext;
                        Log.Information("Vision: annotation edit complete — {Count} annotations, context={Len} chars",
                            editResult.Annotations.Count, editResult.AnnotationContext.Length);

                        // Save annotated image alongside the original
                        string annotatedPath = Path.Combine(visionDir,
                            $"vision_{DateTime.Now:yyyyMMdd_HHmmss}_annotated.{ext}");
                        await File.WriteAllBytesAsync(annotatedPath, imageData).ConfigureAwait(false);
                        Log.Information("Vision: annotated image saved to {Path} ({Size} bytes)",
                            annotatedPath, imageData.Length);
                        // Re-show VisionActionWindow with the annotated image
                        var reActionTcs = new TaskCompletionSource<DiktaMe.Core.Vision.VisionActionResult?>();
                        _uiDispatcher!.TryEnqueue(() =>
                        {
                            _ = ShowVisionActionWindowAsync(reActionTcs, imageData, bounds);
                        });
                        actionResult = await reActionTcs.Task.ConfigureAwait(false);
                        if (actionResult is null || actionResult.Action == DiktaMe.Core.Vision.VisionAction.Edit)
                        {
                            return; // Cancelled or tried to edit again (prevent infinite loop)
                        }

                        // Force cloud provider for post-annotation AI analysis
                        visionProvider = visionSettings.CloudVisionProvider ?? "gemini";
                        visionModelId = visionSettings.CloudVisionModelId ?? "gemini-2.5-flash";
                        Log.Information("Vision: post-edit → running AI analysis with {Provider}/{Model}",
                            visionProvider, visionModelId);

                        // Use user's query from re-shown modal, or default annotation prompt
                        string annotationQuery = !string.IsNullOrWhiteSpace(actionResult.UserQuery)
                            ? actionResult.UserQuery
                            : "Describe this annotated screenshot. Focus on what the annotations highlight or label.";

                        await HandleVisionClipboardAsync(imageData, mimeType, visionProvider, visionModelId, visionSettings,
                            new DiktaMe.Core.Vision.VisionActionResult(DiktaMe.Core.Vision.VisionAction.Clipboard, annotationQuery, UseLocal: false))
                            .ConfigureAwait(false);
                        return;
                    }

                    return;

                case DiktaMe.Core.Vision.VisionAction.Clipboard:
                default:
                    await HandleVisionClipboardAsync(imageData, mimeType, visionProvider, visionModelId, visionSettings, actionResult).ConfigureAwait(false);
                    return;
            }
        }
        catch (Exception ex)
        {
            if (!HandleLicenseError(ex))
            {
                Log.Error(ex, "Vision pipeline failed");
                _notifications.ShowToast("Vision Error", ex.Message, NotificationType.Error, suppressTts: true);
            }
        }
        finally
        {
            _pendingVisionTelemetry = null;
            // Restore audio ducking (Vision triggers Processing state which ducks audio)
            try { await _audioDucker.RestoreAsync().ConfigureAwait(false); }
            catch (Exception ex) { Log.Debug(ex, "Vision: ducking restore failed (non-fatal)"); }
        }
    }

    /// <summary>
    /// Creates and shows the VisionActionWindow on the UI thread, completing the TCS with the result.
    /// Extracted to avoid async-void lambda in DispatcherQueue.TryEnqueue.
    /// </summary>
    private async Task ShowVisionActionWindowAsync(
        TaskCompletionSource<DiktaMe.Core.Vision.VisionActionResult?> tcs,
        byte[] imageData,
        (int X, int Y, int Width, int Height) bounds)
    {
        try
        {
            var actionWindow = new Views.VisionActionWindow();
            await actionWindow.SetThumbnailAsync(imageData).ConfigureAwait(true);
            actionWindow.CenterOnMonitor(bounds);
            actionWindow.Activate();
            var result = await actionWindow.GetResultAsync().ConfigureAwait(true);
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }
    }

    // ── Vision Action Handlers ────────────────────────────────────────────────

    private async Task HandleVisionClipboardAsync(
        byte[] imageData, string mimeType,
        string visionProvider, string visionModelId,
        DiktaMe.Core.Config.VisionSettings visionSettings,
        DiktaMe.Core.Vision.VisionActionResult actionResult)
    {
        // No query → copy raw image to clipboard (no AI)
        if (string.IsNullOrWhiteSpace(actionResult.UserQuery))
        {
            CopyImageToClipboard(imageData);
            _notifications.ShowToast("Vision", "Image copied to clipboard", NotificationType.Success, suppressTts: true);
            return;
        }

        _notifications.ShowToast("Vision", "Analyzing image...", NotificationType.Info, suppressTts: true);
        var options = BuildVisionOptions(visionSettings, visionModelId, actionResult.UserQuery,
            DiktaMe.Core.Vision.VisionOutputMode.Clipboard);

        var result = await RunVisionPipelineCoreAsync(imageData, mimeType, options, visionProvider, visionModelId).ConfigureAwait(false);
        if (result is null)
        {
            return;
        }

        if (result.IsSuccess)
        {
            // Always copy AI text to clipboard
            CopyTextToClipboard(result.Text);
            Log.Information("Vision: Clip result ({Chars} chars) → clipboard", result.Text.Length);

            // Optionally also inject at cursor position
            if (_settings.Current.Vision.ClipInjectAtCursor)
            {
                _textInjector.InjectText(result.Text, trailingSpace: false);
                Log.Information("Vision: also injected at cursor");
            }

            string preview = result.Text.Length > 200
                ? string.Concat(result.Text.AsSpan(0, 200), "...")
                : result.Text;
            _notifications.ShowToast("Vision", preview, NotificationType.Success, suppressTts: true);
        }
        else
        {
            _notifications.ShowToast("Vision Error", result.ErrorMessage ?? "Vision analysis failed",
                NotificationType.Error, suppressTts: true);
        }
    }

    private Task HandleVisionChatAsync(
        byte[] imageData, string mimeType,
        string visionProvider, string visionModelId,
        DiktaMe.Core.Vision.VisionActionResult actionResult)
    {
        // Open QuickChat with image attached — no vision pipeline run here.
        // The user's first chat message goes to the multimodal LLM with the image.
        // Pre-select the model based on the modal's Local/Cloud toggle.
        Log.Information("Vision: opening QuickChat with image ({Size} bytes, {Mime}), provider={Provider}, model={Model}",
            imageData.Length, mimeType, visionProvider, visionModelId);

        string? initialQuery = actionResult.UserQuery;

        _uiDispatcher?.TryEnqueue(() =>
        {
            // Close any existing QuickChat window to ensure a fresh conversation
            App.Current.CloseQuickChat();

            var chatWindow = new Views.QuickChatWindow();
            chatWindow.AttachImage(imageData, mimeType);
            chatWindow.SetInitialModel(visionModelId);
            if (!string.IsNullOrWhiteSpace(initialQuery))
            {
                chatWindow.SetInitialInput(initialQuery, autoSend: true);
            }

            App.Current.TrackQuickChat(chatWindow);
            chatWindow.Activate();
        });

        return Task.CompletedTask;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "Vision+Note orchestrator")]
    private async Task HandleVisionNoteAsync(
        byte[] imageData, string mimeType, string savedImagePath,
        string visionProvider, string visionModelId,
        DiktaMe.Core.Config.VisionSettings visionSettings,
        DiktaMe.Core.Vision.VisionActionResult actionResult)
    {
        // Phase 1: Run vision pipeline to get image description
        _notifications.ShowToast("Vision", "Analyzing image...", NotificationType.Info, suppressTts: true);
        var options = BuildVisionOptions(visionSettings, visionModelId, actionResult.UserQuery,
            DiktaMe.Core.Vision.VisionOutputMode.ToastOnly);

        var visionResult = await RunVisionPipelineCoreAsync(imageData, mimeType, options, visionProvider, visionModelId).ConfigureAwait(false);
        string? visionDescription = visionResult?.IsSuccess == true ? visionResult.Text : null;

        if (visionDescription is not null)
        {
            Log.Information("Vision+Note: got description ({Chars} chars)", visionDescription.Length);
        }

        // Phase 2: Save note directly (no auto-record — user types/dictates query in modal)
        if (visionDescription is null && string.IsNullOrWhiteSpace(actionResult.UserQuery))
        {
            _notifications.ShowToast("Error", "No vision result and no query provided",
                NotificationType.Error, suppressTts: true);
            return;
        }

        await SaveVisionNoteAsync(visionDescription, savedImagePath, actionResult.UserQuery).ConfigureAwait(false);

        Log.Information("Vision+Note: saved to notes");
        _notifications.ShowToast(
            _loc.GetString("Loading_NoteSaved_Title"),
            "Vision note saved",
            NotificationType.Success, spokenKey: "Loading_NoteSaved");
    }

    private async Task SaveVisionNoteAsync(string? visionDescription, string imagePath, string? userQuery)
    {
        string notesPath = _settings.Current.NotesFilePath;
        string? dir = Path.GetDirectoryName(notesPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"## {timestamp}");
        if (!string.IsNullOrWhiteSpace(userQuery))
        {
            sb.AppendLine();
            sb.AppendLine(userQuery.Trim());
        }
        if (!string.IsNullOrWhiteSpace(visionDescription))
        {
            sb.AppendLine();
            sb.AppendLine($"> **Vision**: {visionDescription.Trim()}");
        }
        sb.AppendLine();
        sb.AppendLine($"![capture]({imagePath})");

        await File.AppendAllTextAsync(notesPath, sb.ToString()).ConfigureAwait(false);
        Log.Information("Vision+Note: appended note to {Path}", notesPath);
    }

    /// <summary>
    /// Copies both AI-generated text and the screenshot image to clipboard.
    /// Paste into text editors gets the text; paste into image apps gets the image.
    /// </summary>
    private void CopyTextAndImageToClipboard(string text, byte[] pngData)
    {
        _uiDispatcher?.TryEnqueue(() =>
        {
            try
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(text);
                var stream = new InMemoryRandomAccessStream();
                stream.WriteAsync(pngData.AsBuffer()).AsTask().GetAwaiter().GetResult();
                stream.Seek(0);
                package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                Log.Information("Vision: copied text ({Chars} chars) + image ({Size} bytes) to clipboard", text.Length, pngData.Length);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Vision: failed to copy text+image to clipboard");
            }
        });
    }

    private void CopyTextToClipboard(string text)
    {
        _uiDispatcher?.TryEnqueue(() =>
        {
            try
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                Log.Debug("Vision: copied text ({Chars} chars) to clipboard", text.Length);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Vision: failed to copy text to clipboard");
            }
        });
    }

    private void CopyImageToClipboard(byte[] pngData)
    {
        _uiDispatcher?.TryEnqueue(() =>
        {
            try
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                var stream = new InMemoryRandomAccessStream();
                stream.WriteAsync(pngData.AsBuffer()).AsTask().GetAwaiter().GetResult();
                stream.Seek(0);
                package.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                Log.Information("Vision: copied image ({Size} bytes) to clipboard", pngData.Length);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Vision: failed to copy image to clipboard");
            }
        });
    }

    private async Task HandleVideoRecordAsync(
        (int X, int Y, int Width, int Height) bounds,
        Views.SnippingResult snippingResult)
    {
        // Determine capture region (selected region or full monitor)
        int left, top, width, height;
        if (snippingResult.Mode == DiktaMe.Core.Vision.CaptureMode.Region
            && snippingResult.Region is { } region)
        {
            left = bounds.X + region.X;  // Region is relative to monitor; convert to screen coords
            top = bounds.Y + region.Y;
            width = region.Width;
            height = region.Height;
        }
        else
        {
            left = bounds.X;
            top = bounds.Y;
            width = bounds.Width;
            height = bounds.Height;
        }

        // Prepare output path
        string visionDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiktaMe", "vision");
        Directory.CreateDirectory(visionDir);
        string outputPath = Path.Combine(visionDir,
            $"video_{DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture)}.mp4");

        // Show floating recording bar on UI thread
        var barTcs = new TaskCompletionSource<bool>();
        Views.VideoRecordingBarWindow? recordingBar = null;
        _uiDispatcher!.TryEnqueue(() =>
        {
            recordingBar = new Views.VideoRecordingBarWindow();
            recordingBar.Activate();
            _ = CompleteBarAsync(barTcs, recordingBar);
        });

        // Start capture — read recording settings from AppSettings
        var vs2 = _settings.Current.Vision;
        var (bitrate2, fps2) = vs2.VideoQuality switch
        {
            "low" => (1500, 24),
            "high" => (8000, 60),
            _ => (5000, 30),
        };
        var options = new DiktaMe.Core.Vision.VideoRecordingOptions
        {
            EnableWebcam = vs2.EnableWebcam,
            WebcamBubbleSize = vs2.WebcamSize,
            WebcamPosition = vs2.WebcamPosition,
            BitrateKbps = bitrate2,
            FrameRateHz = fps2,
            EnableMicAudio = vs2.EnableMicAudio,
            EnableSystemAudio = vs2.EnableSystemAudio,
        };
        using var capture = new VideoCapture();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(options.MaxDurationSeconds));

        // When user clicks Stop, cancel the capture
        _ = barTcs.Task.ContinueWith(_ => capture.Stop(), TaskScheduler.Default);

        try
        {
            _notifications.ShowToast("Video", "Recording started...", NotificationType.Info, suppressTts: true);

            var monitorDevice2 = ScreenCapture.GetMonitorDeviceName(left + width / 2, top + height / 2);
            await capture.RecordAsync(
                left, top, width, height,
                outputPath, options, cts.Token, monitorDevice2, bounds.X, bounds.Y).ConfigureAwait(false);

            long fileSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
            Log.Information("Video: recording saved to {Path} ({Size} bytes)", outputPath, fileSize);

            // Clean up recording bar before showing action modal
            _uiDispatcher?.TryEnqueue(() =>
            {
                try { recordingBar?.Close(); }
                catch { /* already closed */ }
            });

            // Show post-recording action modal (V3)
            await HandleVideoPostCaptureAsync(outputPath, fileSize).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException)
        {
            Log.Information("Video: recording stopped (user or max duration)");
            long fileSize = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
            if (fileSize > 0)
            {
                // Clean up recording bar before showing action modal
                _uiDispatcher?.TryEnqueue(() =>
                {
                    try { recordingBar?.Close(); }
                    catch { /* already closed */ }
                });

                await HandleVideoPostCaptureAsync(outputPath, fileSize).ConfigureAwait(false);
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Video: recording failed");
            _notifications.ShowToast("Video Error", ex.Message, NotificationType.Error, suppressTts: true);
        }

        // Clean up recording bar if still open (error/empty cases)
        _uiDispatcher?.TryEnqueue(() =>
        {
            try { recordingBar?.Close(); }
            catch { /* already closed */ }
        });
    }

    private async Task HandleVideoPostCaptureAsync(string videoPath, long fileSize)
    {
        // Show action modal on UI thread
        var actionTcs = new TaskCompletionSource<Views.VideoActionResult>();
        _uiDispatcher?.TryEnqueue(() =>
        {
            _ = ShowVideoActionModalAsync(actionTcs, videoPath, fileSize);
        });

        var actionResult = await actionTcs.Task.ConfigureAwait(false);

        if (actionResult.Action == Views.VideoAiAction.None)
        {
            _notifications.ShowToast("Video",
                $"Saved ({fileSize / 1024}KB) → {Path.GetFileName(videoPath)}",
                NotificationType.Success, suppressTts: true);
            return;
        }

        // Run Gemini video understanding via VisionPipeline (reuses cloud vision provider)
        var vSettings = _settings.Current.Vision;
        string defaultQuery = actionResult.CustomPrompt ?? actionResult.Action switch
        {
            Views.VideoAiAction.Describe => vSettings.VideoDescribePrompt,
            Views.VideoAiAction.Document => vSettings.VideoDocumentPrompt,
            Views.VideoAiAction.BugReport => vSettings.VideoBugReportPrompt,
            _ => vSettings.VideoDescribePrompt,
        };

        _notifications.ShowToast("Video AI", "Analyzing video with Gemini...", NotificationType.Info, suppressTts: true);
        Log.Information("Video AI: running {Action} on {Path} ({Size}KB)", actionResult.Action, videoPath, fileSize / 1024);

        try
        {
            byte[] videoData = await File.ReadAllBytesAsync(videoPath).ConfigureAwait(false);

            // Video analysis is cloud-only — local models (minicpm-v) crash on raw MP4 bytes.
            // Local video would need keyframe extraction (1 frame/sec → multiple images).
            // TODO(V3-LOCAL): Extract keyframes + multi-image Ollama call for local video understanding
            var appSettings = _settings.Current;
            string provider = appSettings.Vision.CloudVisionProvider ?? "gemini";
            string model = appSettings.Vision.CloudVisionModelId ?? "gemini-2.5-flash";
            Log.Information("Video AI: using {Provider}/{Model} (cloud-only)", provider, model);
            var pipeline = _pipelineFactory.CreateVisionPipeline(provider, model);

            var visionOptions = new DiktaMe.Core.Vision.VisionOptions
            {
                DefaultQuery = defaultQuery,
                SystemPrompt = vSettings.VideoSystemPrompt,
            };

            var result = await pipeline.RunAsync(
                videoData, "video/mp4",
                audioFilePath: null,
                visionOptions,
                CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Text))
            {
                ClipboardManager.SetText(result.Text);
                Log.Information("Video AI: {Action} complete — {Chars} chars, {Ms}ms", actionResult.Action, result.Text.Length, result.TotalMs);
                _notifications.ShowToast("Video AI",
                    $"{actionResult.Action} copied to clipboard ({result.Text.Length} chars)",
                    NotificationType.Success, suppressTts: true);
            }
            else
            {
                string errorMsg = result.ErrorMessage ?? "No response from Gemini.";
                _notifications.ShowToast("Video AI", errorMsg, NotificationType.Warning, suppressTts: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Video AI: {Action} failed", actionResult.Action);
            _notifications.ShowToast("Video AI Error", ex.Message, NotificationType.Error, suppressTts: true);
        }
    }

    private async Task ShowVideoActionModalAsync(
        TaskCompletionSource<Views.VideoActionResult> tcs, string videoPath, long fileSize)
    {
        try
        {
            var modal = new Views.VideoActionWindow();
            modal.SetFileInfo(Path.GetFileName(videoPath), fileSize);
            modal.Activate();
            var result = await modal.GetResultAsync();
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "VideoAction: modal failed");
            tcs.TrySetResult(new Views.VideoActionResult(Views.VideoAiAction.None, null));
        }
    }

    private static async Task CompleteBarAsync(TaskCompletionSource<bool> tcs, Views.VideoRecordingBarWindow bar)
    {
        bool result = await bar.WaitForStopAsync().ConfigureAwait(true);
        tcs.TrySetResult(result);
    }

    /// <summary>
    /// Logs a vision capture that didn't go through AI (Save, Color) to history.db.
    /// </summary>
    private async Task LogVisionOnlyAsync(
        string mode, long captureMs,
        DiktaMe.Core.Vision.CaptureMode captureMode, DiktaMe.Core.Vision.VisionAction action,
        int imageWidth, int imageHeight)
    {
        var result = new DiktaMe.Core.Pipeline.PipelineResult
        {
            Text = string.Empty,
            Mode = mode,
            IsSuccess = true,
            CaptureMode = captureMode.ToString(),
            ActionType = action.ToString(),
            ImageWidth = imageWidth,
            ImageHeight = imageHeight,
            CaptureMs = captureMs,
        };
        await _history.LogSessionAsync(result).ConfigureAwait(false);
    }

    private async Task HandleVisionColorPickAsync(byte[] imageData)
    {
        // Re-open a color picker overlay using the same captured screenshot.
        // Must create WinUI Window on UI thread — use TaskCompletionSource to bridge.
        var colorTcs = new TaskCompletionSource<List<Views.ColorPickResult>?>();

        _uiDispatcher?.TryEnqueue(() =>
        {
            _ = ShowColorPickerOverlayAsync(colorTcs, imageData);
        });

        var palette = await colorTcs.Task.ConfigureAwait(false);
        if (palette is null || palette.Count == 0)
        {
            return; // Cancelled
        }

        bool injectColor = _settings.Current.Vision.ColorPickerInjectAtCursor;

        if (palette.Count == 1)
        {
            // Single pick
            var c = palette[0];
            ClipboardManager.SetText(c.Hex);
            if (injectColor)
            {
                _textInjector.InjectText(c.Hex, trailingSpace: false);
            }

            Log.Information("ColorPicker: {Hex} → clipboard{Inject}", c.Hex, injectColor ? " + cursor" : "");
            _notifications.ShowToast("Color Picked", $"{c.Hex}  —  rgb({c.R}, {c.G}, {c.B})",
                NotificationType.Success, suppressTts: true);
        }
        else if (_colorPickerAnalyzeRequested)
        {
            // Multi-pick palette + AI analysis
            var lines = palette.Select(c => $"{c.Hex}  rgb({c.R}, {c.G}, {c.B})");
            var paletteText = string.Join(Environment.NewLine, lines);
            ClipboardManager.SetText(paletteText);
            if (injectColor)
            {
                _textInjector.InjectText(paletteText, trailingSpace: false);
            }

            Log.Information("ColorPicker: palette of {Count} colors — running AI analysis", palette.Count);
            _notifications.ShowToast("Palette", "Analyzing palette with AI...", NotificationType.Info, suppressTts: true);
            await AnalyzePaletteAsync(palette).ConfigureAwait(false);
        }
        else
        {
            // Multi-pick palette — copy all as formatted text
            var lines = palette.Select(c => $"{c.Hex}  rgb({c.R}, {c.G}, {c.B})");
            var paletteText = string.Join(Environment.NewLine, lines);
            ClipboardManager.SetText(paletteText);
            if (injectColor)
            {
                _textInjector.InjectText(paletteText, trailingSpace: false);
            }

            Log.Information("ColorPicker: palette of {Count} colors → clipboard{Inject}", palette.Count, injectColor ? " + cursor" : "");
            _notifications.ShowToast("Palette Copied",
                $"{palette.Count} colors copied to clipboard",
                NotificationType.Success, suppressTts: true);
        }
    }

    private async Task AnalyzePaletteAsync(List<Views.ColorPickResult> palette)
    {
        try
        {
            var hexList = string.Join(", ", palette.Select(c => c.Hex));
            var prompt = $"Palette: {hexList}\nFor each: name, then style summary, WCAG AA text pairs, CSS vars, 2 complementary colors. Be concise, no markdown headers.";

            var appSettings = _settings.Current;
            string provider = appSettings.Vision.CloudVisionProvider ?? "gemini";
            string model = appSettings.Vision.CloudVisionModelId ?? "gemini-2.5-flash";
            Log.Information("Palette AI: analyzing {Count} colors with {Provider}/{Model}", palette.Count, provider, model);

            // Use vision pipeline (4096 maxOutputTokens) instead of text pipeline (1024) —
            // palette analysis needs room for color names + accessibility + CSS + suggestions.
            var pipeline = _pipelineFactory.CreateVisionPipeline(provider, model);
            var visionOptions = new DiktaMe.Core.Vision.VisionOptions
            {
                DefaultQuery = prompt,
                SystemPrompt = "You are a design system expert. Analyze color palettes concisely and practically.",
            };

            // Send palette text as a minimal 1x1 PNG to use the image/vision API path
            byte[] minimalPng = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
            var result = await pipeline.RunAsync(
                minimalPng, "image/png",
                audioFilePath: null,
                visionOptions,
                CancellationToken.None).ConfigureAwait(false);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Text))
            {
                ClipboardManager.SetText(result.Text);
                Log.Information("Palette AI: analysis complete — {Chars} chars, {Ms}ms", result.Text.Length, result.TotalMs);
                _notifications.ShowToast("Palette Analysis",
                    $"Analysis copied to clipboard ({result.Text.Length} chars)",
                    NotificationType.Success, suppressTts: true);
            }
            else
            {
                string errorMsg = result.ErrorMessage ?? "No response from AI.";
                _notifications.ShowToast("Palette Analysis", errorMsg, NotificationType.Warning, suppressTts: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Palette AI: analysis failed");
            _notifications.ShowToast("Palette Error", ex.Message, NotificationType.Error, suppressTts: true);
        }
    }

    private bool _colorPickerAnalyzeRequested;
    private string? _annotationContext;

    private async Task<Views.AnnotationResult?> HandleAnnotationEditAsync(byte[] imageData)
    {
        var tcs = new TaskCompletionSource<Views.AnnotationResult?>();
        _uiDispatcher?.TryEnqueue(() => _ = OpenAnnotationEditorAsync(tcs, imageData));
        return await tcs.Task.ConfigureAwait(false);
    }

    private static async Task OpenAnnotationEditorAsync(
        TaskCompletionSource<Views.AnnotationResult?> tcs, byte[] imageData)
    {
        try
        {
            var editor = new Views.AnnotationWindow();
            await editor.SetImageAsync(imageData);
            editor.Activate();
            var result = await editor.GetResultAsync();
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "AnnotationWindow: failed");
            tcs.TrySetResult(null);
        }
    }

    private async Task ShowColorPickerOverlayAsync(
        TaskCompletionSource<List<Views.ColorPickResult>?> tcs, byte[] imageData)
    {
        try
        {
            var picker = new Views.ColorPickerOverlayWindow();
            var bounds = DiktaMe.Core.Vision.ScreenCapture.GetActiveMonitorBounds();
            picker.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            await picker.SetBackgroundScreenshotAsync(imageData);
            picker.Activate();
            var result = await picker.GetResultAsync();
            _colorPickerAnalyzeRequested = picker.AnalyzeRequested;
            tcs.TrySetResult(result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ColorPicker: overlay failed");
            tcs.TrySetResult(null);
        }
    }

    private async Task HandleVisionOcrAsync(
        byte[] imageData, string mimeType,
        string visionProvider, string visionModelId,
        DiktaMe.Core.Config.VisionSettings visionSettings,
        DiktaMe.Core.Vision.VisionActionResult actionResult)
    {
        _notifications.ShowToast("Vision", "Extracting text (OCR)...", NotificationType.Info, suppressTts: true);

        var options = BuildVisionOptions(visionSettings, visionModelId,
            visionSettings.OcrPrompt,
            DiktaMe.Core.Vision.VisionOutputMode.Clipboard);

        var result = await RunVisionPipelineCoreAsync(imageData, mimeType, options, visionProvider, visionModelId).ConfigureAwait(false);
        if (result is null)
        {
            return;
        }

        if (result.IsSuccess)
        {
            // Always copy to clipboard
            ClipboardManager.SetText(result.Text);
            Log.Information("Vision OCR: copied {Chars} chars to clipboard", result.Text.Length);

            // Optionally also inject at cursor
            if (_settings.Current.Vision.OcrInjectAtCursor)
            {
                _textInjector.InjectText(result.Text, trailingSpace: false);
                Log.Information("Vision OCR: also injected at cursor");
            }

            string preview = result.Text.Length > 200
                ? string.Concat(result.Text.AsSpan(0, 200), "...")
                : result.Text;
            _notifications.ShowToast("OCR", preview, NotificationType.Success, suppressTts: true);
        }
        else
        {
            _notifications.ShowToast("OCR Error", result.ErrorMessage ?? "OCR extraction failed",
                NotificationType.Error, suppressTts: true);
        }
    }

    private async Task HandleVisionTableAsync(
        byte[] imageData, string mimeType,
        string visionProvider, string visionModelId,
        DiktaMe.Core.Config.VisionSettings visionSettings,
        DiktaMe.Core.Vision.VisionActionResult actionResult)
    {
        _notifications.ShowToast("Vision", "Extracting table data...", NotificationType.Info, suppressTts: true);

        var options = BuildVisionOptions(visionSettings, visionModelId,
            visionSettings.TablePrompt,
            DiktaMe.Core.Vision.VisionOutputMode.Clipboard);

        var result = await RunVisionPipelineCoreAsync(imageData, mimeType, options, visionProvider, visionModelId).ConfigureAwait(false);
        if (result is null)
        {
            return;
        }

        if (result.IsSuccess)
        {
            ClipboardManager.SetText(result.Text);
            Log.Information("Vision Table: copied {Chars} chars of TSV to clipboard", result.Text.Length);
            _notifications.ShowToast("Table", $"Copied {result.Text.Split('\n').Length} rows to clipboard",
                NotificationType.Success, suppressTts: true);
        }
        else
        {
            _notifications.ShowToast("Table Error", result.ErrorMessage ?? "Table extraction failed",
                NotificationType.Error, suppressTts: true);
        }
    }

    /// <summary>
    /// Shows a FileSavePicker for the user to choose where to save the screenshot.
    /// Falls back to clipboard-only if picker is cancelled.
    /// </summary>
    private async Task SaveVisionWithPickerAsync(string autoSavedPath, byte[] imageData)
    {
        var tcs = new TaskCompletionSource<string?>();
        _uiDispatcher!.TryEnqueue(() =>
        {
            _ = ShowSavePickerAsync(tcs, autoSavedPath);
        });

        string? chosenPath = await tcs.Task.ConfigureAwait(false);
        if (chosenPath is not null)
        {
            _notifications.ShowToast("Vision", $"Saved to {Path.GetFileName(chosenPath)} & copied to clipboard",
                NotificationType.Success, suppressTts: true);
        }
        else
        {
            _notifications.ShowToast("Vision", "Image copied to clipboard",
                NotificationType.Success, suppressTts: true);
        }
    }

    private async Task ShowSavePickerAsync(TaskCompletionSource<string?> tcs, string autoSavedPath)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedFileName = Path.GetFileName(autoSavedPath);
            picker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" });
            picker.FileTypeChoices.Add("JPEG Image", new List<string> { ".jpg" });

            var mainWindow = App.Current.MainWindow;
            if (mainWindow is not null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var file = await picker.PickSaveFileAsync();
            if (file is not null)
            {
                // Copy the auto-saved file to the user-chosen location
                File.Copy(autoSavedPath, file.Path, overwrite: true);
                tcs.TrySetResult(file.Path);
            }
            else
            {
                tcs.TrySetResult(null);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Vision: FileSavePicker failed");
            tcs.TrySetResult(null);
        }
    }

    /// <summary>
    /// Builds the PreCapturedContext markdown block for a vision+note entry.
    /// Always includes the query and image link; vision description is optional.
    /// </summary>
    private static string BuildVisionNoteContext(string? userQuery, string? visionDescription, string imagePath)
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(userQuery))
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"**Query**: {userQuery}");
        }

        if (!string.IsNullOrWhiteSpace(visionDescription))
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"**Vision**: {visionDescription}");
        }

        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"\n![capture]({imagePath})");
        return sb.ToString().TrimEnd();
    }

    private string ResolveVisionFolder()
    {
        string folder = _settings.Current.Vision.SaveFolder;
        if (!string.IsNullOrWhiteSpace(folder))
        {
            return folder;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DiktaMe", "vision");
    }

    private DiktaMe.Core.Vision.VisionOptions BuildVisionOptions(
        DiktaMe.Core.Config.VisionSettings visionSettings,
        string visionModelId,
        string? userQuery,
        DiktaMe.Core.Vision.VisionOutputMode outputMode)
    {
        // Use provider-specific system prompt from settings
        bool isCloud = !string.Equals(visionSettings.VisionProvider, "ollama", StringComparison.OrdinalIgnoreCase);
        string systemPrompt = isCloud ? visionSettings.CloudSystemPrompt : visionSettings.LocalSystemPrompt;

        // Inject annotation context if the user marked up the screenshot
        if (!string.IsNullOrEmpty(_annotationContext))
        {
            systemPrompt = $"{systemPrompt}\n\n{_annotationContext}";
            Log.Debug("Vision: injected annotation context ({Len} chars) into system prompt", _annotationContext.Length);
        }

        return new DiktaMe.Core.Vision.VisionOptions
        {
            SystemPrompt = systemPrompt,
            ModelName = string.IsNullOrWhiteSpace(visionModelId) ? null : visionModelId,
            DefaultQuery = !string.IsNullOrWhiteSpace(userQuery)
                ? userQuery
                : visionSettings.DefaultQuery,
            MaxImageDimensionPx = visionSettings.MaxImageDimensionPx,
            MaxResponseTokens = visionSettings.MaxResponseTokens,
            Temperature = visionSettings.Temperature,
            OutputMode = outputMode,
        };
    }

    private async Task<DiktaMe.Core.Pipeline.PipelineResult?> RunVisionPipelineCoreAsync(
        byte[] imageData, string mimeType, DiktaMe.Core.Vision.VisionOptions options,
        string? providerOverride = null, string? modelOverride = null)
    {
        var pipeline = (providerOverride is not null && modelOverride is not null)
            ? _pipelineFactory.CreateVisionPipeline(providerOverride, modelOverride)
            : _pipelineFactory.CreateVisionPipeline();
        pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
        using var cts = new CancellationTokenSource();

        var result = await pipeline.RunAsync(imageData, mimeType, audioFilePath: null, options, cts.Token)
            .ConfigureAwait(false);

        // Enrich with vision telemetry if available
        if (_pendingVisionTelemetry is not null)
        {
            var t = _pendingVisionTelemetry.Value;
            result = result with
            {
                CaptureMode = t.CaptureMode,
                ActionType = t.ActionType,
                ImageWidth = t.ImageWidth,
                ImageHeight = t.ImageHeight,
                CaptureMs = t.CaptureMs,
            };
        }

        _controlPanel.OnPipelineCompleted(this, result);
        _pipelineEventBus.PublishCompleted(result);

        if (result.IsSuccess)
        {
            await _history.LogSessionAsync(result).ConfigureAwait(false);
            Log.Information("Vision: Success via {Provider} in {Ms}ms — {Chars} chars",
                result.LlmProvider, result.ProcessingMs, result.Text.Length);
        }
        else
        {
            Log.Warning("Vision: failed via {Provider} — {Error}", result.LlmProvider, result.ErrorMessage);
        }

        return result;
    }
}

/// <summary>Win32 P/Invoke helpers for keyboard polling.</summary>
internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    internal static extern short GetAsyncKeyState(int vKey);
}
