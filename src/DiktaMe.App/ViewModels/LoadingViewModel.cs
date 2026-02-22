
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Input;
using DiktaMe.Core.SystemManagement;
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

    [ObservableProperty] private string _statusText = "Initializing...";
    [ObservableProperty] private double _progress;

    public event Action? LoadingComplete;

    public LoadingViewModel(
        SettingsManager settings,
        HistoryManager history,
        SnippetManager snippets,
        OllamaManager ollama,
        HotkeyManager hotkeyManager)
    {
        _settings = settings;
        _history = history;
        _snippets = snippets;
        _ollama = ollama;
        _hotkeyManager = hotkeyManager;
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
            // Start the background message pump
            _hotkeyManager.Start();

            // Subscribe to events
            _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
            _hotkeyManager.RegistrationFailed += OnHotkeyRegistrationFailed;

            // Register all configured hotkeys
            RegisterAllHotkeys(_settings.Current.Hotkeys);

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
        _hotkeyManager.Register(HotkeyId.Dictate, hotkeys.Dictate);
        _hotkeyManager.Register(HotkeyId.Refine, hotkeys.Refine);
        _hotkeyManager.Register(HotkeyId.Ask, hotkeys.Ask);
        _hotkeyManager.Register(HotkeyId.Translate, hotkeys.Translate);
        _hotkeyManager.Register(HotkeyId.Oops, hotkeys.Oops);
        _hotkeyManager.Register(HotkeyId.Note, hotkeys.Note);
        _hotkeyManager.Register(HotkeyId.Chat, hotkeys.Chat);
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        Log.Information("Hotkey pressed: {Id}", e.Id);

        // Dispatch to UI thread for window operations
        // Get DispatcherQueue from the current window if available
        var dispatcherQueue = App.Current.MainWindow?.DispatcherQueue
            ?? DispatcherQueue.GetForCurrentThread();

        dispatcherQueue?.TryEnqueue(() =>
        {
            try
            {
                switch (e.Id)
                {
                    case HotkeyId.Chat:
                        // Open Quick Chat window
                        App.Current.ToggleQuickChat();
                        break;

                    case HotkeyId.Dictate:
                    case HotkeyId.Refine:
                    case HotkeyId.Ask:
                    case HotkeyId.Translate:
                    case HotkeyId.Oops:
                    case HotkeyId.Note:
                        // Pipeline integration pending - will be connected when PipelineOrchestrator is implemented
                        Log.Warning("Hotkey {Id} triggered but pipeline integration not yet implemented", e.Id);
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

        // NOTE: User notification for hotkey conflicts will be added when NotificationService is wired up
    }
}
