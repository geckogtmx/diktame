namespace DiktaMe.App.ViewModels;

using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.SystemManagement;
using Serilog;

public sealed partial class LoadingViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly HistoryManager _history;
    private readonly SnippetManager _snippets;
    private readonly OllamaManager _ollama;

    [ObservableProperty] private string _statusText = "Initializing...";
    [ObservableProperty] private double _progress;

    public event Action? LoadingComplete;

    public LoadingViewModel(
        SettingsManager settings,
        HistoryManager history,
        SnippetManager snippets,
        OllamaManager ollama)
    {
        _settings = settings;
        _history = history;
        _snippets = snippets;
        _ollama = ollama;
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
}
