
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
using DiktaMe.Plugin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Serilog;


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
                        _ = RunVisionPipelineAsync();
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
        // Wallet mode forces batch path — STTRouter/LLMRouter handle proxy routing.
        if (_settings.Current.General.StreamingEnabled
            && _pipelineFactory.CanStreamDictation()
            && _settings.Current.AuthMode != AuthMode.Wallet)
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
            Log.Error(ex, "Refine Auto pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
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
                    NotificationType.Warning);
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
            Log.Error(ex, "ReadSelection pipeline failed");
            _notifications.ShowToast("Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }

    // ── Vision Pipeline (SPEC_015-0C) ────────────────────────────────────

    private async Task RunVisionPipelineAsync()
    {
        try
        {
            Log.Information("Starting Vision pipeline...");

            // Step 1: Capture the active window BEFORE showing overlay
            var bounds = DiktaMe.Core.Vision.ScreenCapture.GetActiveWindowBounds();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                _notifications.ShowToast("Vision", "No active window found", NotificationType.Error);
                return;
            }

            Log.Debug("Vision: capturing active window ({W}x{H} at {X},{Y})",
                bounds.Width, bounds.Height, bounds.X, bounds.Y);
            byte[] windowPng = await Task.Run(DiktaMe.Core.Vision.ScreenCapture.CaptureActiveWindow)
                .ConfigureAwait(true);
            Log.Debug("Vision: active window captured ({Size} bytes)", windowPng.Length);

            // Step 2: Show snipping overlay sized to the active window
            var overlay = new Views.SnippingOverlayWindow();
            overlay.SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            await overlay.SetBackgroundScreenshotAsync(windowPng).ConfigureAwait(true);
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

            var (imageData, mimeType) = await Task.Run(() =>
            {
                byte[] screenshot;
                if (snippingResult.Mode == DiktaMe.Core.Vision.CaptureMode.Region
                    && snippingResult.Region is { } region)
                {
                    // Region coordinates are relative to the overlay (which matches the window)
                    // Translate to screen coordinates by adding window origin
                    screenshot = DiktaMe.Core.Vision.ScreenCapture.CaptureRegion(
                        bounds.X + region.X, bounds.Y + region.Y, region.Width, region.Height);
                }
                else
                {
                    screenshot = windowPng;
                }

                Log.Debug("Vision: captured {Size} bytes, preparing for API", screenshot.Length);
                return DiktaMe.Core.Vision.ImageProcessor.PrepareForApi(
                    screenshot, visionSettings.MaxImageDimensionPx);
            }).ConfigureAwait(false);

            if (imageData.Length == 0)
            {
                _notifications.ShowToast("Vision", "Screen capture failed", NotificationType.Error);
                return;
            }

            // Save screenshot for debugging
            string visionDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DiktaMe", "vision");
            Directory.CreateDirectory(visionDir);
            string ext = string.Equals(mimeType, "image/jpeg", StringComparison.Ordinal) ? "jpg" : "png";
            string savedPath = Path.Combine(visionDir,
                $"vision_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
            await File.WriteAllBytesAsync(savedPath, imageData).ConfigureAwait(false);
            Log.Information("Vision: image saved to {Path} ({MimeType}, {Size} bytes)",
                savedPath, mimeType, imageData.Length);

            // Step 4: Create and run vision pipeline
            Log.Information("Vision: creating pipeline and sending to LLM...");
            _notifications.ShowToast("Vision", "Analyzing image...", NotificationType.Info);
            var options = new DiktaMe.Core.Vision.VisionOptions
            {
                SystemPrompt = "You are a concise vision assistant. Respond briefly and directly. Do not describe the UI chrome, window decorations, or layout — focus only on the meaningful content. Keep responses under 200 words unless the user asks for more detail.",
                DefaultQuery = visionSettings.DefaultQuery,
                MaxImageDimensionPx = visionSettings.MaxImageDimensionPx,
                MaxResponseTokens = visionSettings.MaxResponseTokens,
                Temperature = visionSettings.Temperature,
                OutputMode = string.Equals(visionSettings.OutputMode, "clipboard", StringComparison.OrdinalIgnoreCase)
                    ? DiktaMe.Core.Vision.VisionOutputMode.Clipboard
                    : string.Equals(visionSettings.OutputMode, "toast", StringComparison.OrdinalIgnoreCase)
                        ? DiktaMe.Core.Vision.VisionOutputMode.ToastOnly
                        : DiktaMe.Core.Vision.VisionOutputMode.Inject,
            };

            var pipeline = _pipelineFactory.CreateVisionPipeline();
            pipeline.StateChanged += _controlPanel.OnPipelineStateChanged;
            _recordingCts = new CancellationTokenSource();

            var result = await pipeline.RunAsync(imageData, mimeType, audioFilePath: null, options, _recordingCts.Token)
                .ConfigureAwait(false);

            _controlPanel.OnPipelineCompleted(this, result);
            _pipelineEventBus.PublishCompleted(result);

            if (result.IsSuccess)
            {
                string preview = result.Text.Length > 200
                    ? string.Concat(result.Text.AsSpan(0, 200), "...")
                    : result.Text;
                Log.Information("Vision: Success via {Provider} in {Ms}ms — {Chars} chars, {InTok}→{OutTok} tokens",
                    result.LlmProvider, result.ProcessingMs, result.Text.Length,
                    result.InputTokens, result.OutputTokens);
                Log.Debug("Vision response: {Text}", result.Text);

                // Store in history (via ControlPanel → MetricsCollector → HistoryManager)
                await _history.LogSessionAsync(result).ConfigureAwait(false);

                _notifications.ShowToast("Vision", preview, NotificationType.Success);
            }
            else
            {
                Log.Warning("Vision: failed via {Provider} — {Error}", result.LlmProvider, result.ErrorMessage);
                _notifications.ShowToast("Vision Error", result.ErrorMessage ?? "Vision analysis failed", NotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Vision pipeline failed");
            _notifications.ShowToast("Vision Error", ex.Message, NotificationType.Error);
        }
        finally
        {
            // Restore audio ducking (Vision triggers Processing state which ducks audio)
            try { await _audioDucker.RestoreAsync().ConfigureAwait(false); }
            catch (Exception ex) { Log.Debug(ex, "Vision: ducking restore failed (non-fatal)"); }

            _recordingCts?.Dispose();
            _recordingCts = null;
        }
    }
}
