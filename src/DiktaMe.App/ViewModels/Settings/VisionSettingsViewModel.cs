
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// ViewModel for the Vision settings page (SPEC_002).
/// </summary>
public sealed partial class VisionSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;

    public string[] OutputModeOptions { get; } = ["Inject at cursor", "Copy to clipboard", "Toast only"];
    public string[] ProviderOptions { get; } = ["gemini", "anthropic", "openai", "ollama"];

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _defaultQuery = "";

    [ObservableProperty]
    private bool _autoRecordQuery;

    [ObservableProperty]
    private int _selectedOutputModeIndex;

    [ObservableProperty]
    private double _temperature;

    [ObservableProperty]
    private int _maxResponseTokens;

    [ObservableProperty]
    private int _ollamaKeepAliveSeconds;

    [ObservableProperty]
    private int _selectedProviderIndex;

    [ObservableProperty]
    private string _visionModelId = "";

    public VisionSettingsViewModel(SettingsManager settings)
    {
        _settings = settings;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        var v = _settings.Current.Vision;
        Enabled = v.Enabled;
        DefaultQuery = v.DefaultQuery;
        AutoRecordQuery = v.AutoRecordQuery;
        Temperature = v.Temperature;
        MaxResponseTokens = v.MaxResponseTokens;
        OllamaKeepAliveSeconds = v.OllamaKeepAliveSeconds;
        VisionModelId = v.VisionModelId;

        SelectedOutputModeIndex = v.OutputMode switch
        {
            "clipboard" => 1,
            "toast" => 2,
            _ => 0,
        };

        SelectedProviderIndex = Array.IndexOf(ProviderOptions, v.VisionProvider);
        if (SelectedProviderIndex < 0)
        {
            SelectedProviderIndex = 0;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        string outputMode = SelectedOutputModeIndex switch
        {
            1 => "clipboard",
            2 => "toast",
            _ => "inject",
        };

        string provider = SelectedProviderIndex >= 0 && SelectedProviderIndex < ProviderOptions.Length
            ? ProviderOptions[SelectedProviderIndex]
            : "gemini";

        var updated = _settings.Current with
        {
            Vision = _settings.Current.Vision with
            {
                Enabled = Enabled,
                DefaultQuery = DefaultQuery,
                AutoRecordQuery = AutoRecordQuery,
                OutputMode = outputMode,
                Temperature = Temperature,
                MaxResponseTokens = MaxResponseTokens,
                OllamaKeepAliveSeconds = OllamaKeepAliveSeconds,
                VisionProvider = provider,
                VisionModelId = VisionModelId,
            },
        };

        await _settings.UpdateAsync(updated).ConfigureAwait(false);
        Log.Information("VisionSettingsViewModel: Settings saved");
    }
}
