namespace DiktaMe.App.ViewModels.Settings;

using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.Core.Config;

public sealed partial class AIEngineSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;

    [ObservableProperty]
    private int _sttModeIndex; // 0 = Cloud, 1 = Local

    [ObservableProperty]
    private int _llmModeIndex; // 0 = Cloud, 1 = Local (Ollama), 2 = Skip

    [ObservableProperty]
    private string _capabilitySummary = "";

    public AIEngineSettingsViewModel(SettingsManager settings)
    {
        _settings = settings;
        LoadCapabilitySummary();
    }

    public string[] SttModes { get; } = ["Cloud (Deepgram / Gemini)", "Local (Whisper)"];
    public string[] LlmModes { get; } = ["Cloud (Gemini / OpenAI / Anthropic)", "Local (Ollama)", "Skip LLM"];

    private void LoadCapabilitySummary()
    {
        var s = _settings.Current;
        var defaultMode = s.ModeProfiles.GetValueOrDefault("dictate_0", new ModeSettings());
        var sttLabel = defaultMode.SttProvider switch
        {
            "whisper" => "Local Whisper",
            "deepgram" => "Deepgram Cloud",
            "gemini-audio" => "Gemini Audio",
            _ => defaultMode.SttProvider,
        };
        var llmLabel = defaultMode.LlmProvider switch
        {
            "ollama" => $"Ollama ({s.OllamaModel})",
            "none" => "Disabled",
            _ => defaultMode.LlmProvider,
        };

        SttModeIndex = defaultMode.SttProvider == "whisper" ? 1 : 0;
        LlmModeIndex = defaultMode.LlmProvider switch
        {
            "ollama" => 1,
            "none" => 2,
            _ => 0,
        };

        CapabilitySummary = $"STT: {sttLabel}  |  LLM: {llmLabel}";
    }
}
