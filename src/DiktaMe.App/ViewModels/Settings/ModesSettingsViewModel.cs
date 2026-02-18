
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class ModesSettingsViewModel : ObservableObject
{
    private readonly ProfileManager _profileManager;
    private readonly SettingsManager _settings;
    private bool _isLoading;

    // ── Mode list ───────────────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedModeIndex;

    public string[] ModeNames { get; } = ["Dictate", "Refine", "Ask", "Translate", "Note", "Chat"];
    public string[] ModeCodes { get; } = ["dictate", "refine", "ask", "translate", "note", "chat"];

    // ── Profile selector ────────────────────────────────────────────────────

    [ObservableProperty]
    private int _activeProfileIndex;

    public string[] ProfileLabels { get; } = ["Profile A (Cloud)", "Profile B (Local)"];

    // ── Mode detail (right pane) ────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedSttProviderIndex;

    [ObservableProperty]
    private int _selectedLlmProviderIndex;

    [ObservableProperty]
    private string _llmModel = "";

    [ObservableProperty]
    private int _promptSlot = -1;

    [ObservableProperty]
    private bool _useLlm = true;

    public string[] SttProviders { get; } = ["deepgram", "gemini-audio", "whisper"];
    public string[] SttProviderLabels { get; } = ["Deepgram (Cloud)", "Gemini Audio (Cloud)", "Whisper (Local)"];
    public string[] LlmProviders { get; } = ["gemini", "openai", "anthropic", "ollama", "none"];
    public string[] LlmProviderLabels { get; } = ["Gemini", "OpenAI", "Anthropic", "Ollama (Local)", "None (Skip)"];
    public string[] PromptSlotLabels { get; } = BuildPromptSlotLabels();

    public ModesSettingsViewModel(ProfileManager profileManager, SettingsManager settings)
    {
        _profileManager = profileManager;
        _settings = settings;
        _activeProfileIndex = _settings.Current.ActiveProfile;
        LoadModeDetail();
    }

    partial void OnSelectedModeIndexChanged(int value) => LoadModeDetail();
    partial void OnActiveProfileIndexChanged(int value)
    {
        _ = _profileManager.SwitchProfileAsync(value);
        LoadModeDetail();
    }

    partial void OnSelectedSttProviderIndexChanged(int value) => SaveModeDetail();
    partial void OnSelectedLlmProviderIndexChanged(int value) => SaveModeDetail();
    partial void OnLlmModelChanged(string value) => SaveModeDetail();
    partial void OnPromptSlotChanged(int value) => SaveModeDetail();
    partial void OnUseLlmChanged(bool value) => SaveModeDetail();

    [RelayCommand]
    private void SaveModel()
    {
        SaveModeDetail();
    }

    private void LoadModeDetail()
    {
        _isLoading = true;

        string mode = SelectedModeIndex >= 0 && SelectedModeIndex < ModeCodes.Length
            ? ModeCodes[SelectedModeIndex] : "dictate";

        var ms = _profileManager.GetModeSettings(mode, ActiveProfileIndex);

        SelectedSttProviderIndex = Array.IndexOf(SttProviders, ms.SttProvider) is var si and >= 0 ? si : 0;
        SelectedLlmProviderIndex = Array.IndexOf(LlmProviders, ms.LlmProvider) is var li and >= 0 ? li : 0;
        LlmModel = ms.LlmModel;
        PromptSlot = ms.PromptSlot + 1; // shift: -1 becomes index 0 ("Default")
        UseLlm = ms.UseLlm;

        _isLoading = false;
    }

    private void SaveModeDetail()
    {
        if (_isLoading)
        {
            return;
        }

        string mode = SelectedModeIndex >= 0 && SelectedModeIndex < ModeCodes.Length
            ? ModeCodes[SelectedModeIndex] : "dictate";

        var ms = new ModeSettings
        {
            SttProvider = SelectedSttProviderIndex >= 0 && SelectedSttProviderIndex < SttProviders.Length
                ? SttProviders[SelectedSttProviderIndex] : "deepgram",
            LlmProvider = SelectedLlmProviderIndex >= 0 && SelectedLlmProviderIndex < LlmProviders.Length
                ? LlmProviders[SelectedLlmProviderIndex] : "gemini",
            LlmModel = LlmModel,
            PromptSlot = PromptSlot - 1, // shift back: index 0 becomes -1 ("Default")
            UseLlm = UseLlm,
        };

        _ = _profileManager.SetModeSettingsAsync(mode, ActiveProfileIndex, ms).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save mode settings for {Mode}", mode);
            }
        }, TaskScheduler.Default);
    }

    private static string[] BuildPromptSlotLabels()
    {
        var labels = new string[17]; // -1 (default) + 0-15
        labels[0] = "Default (provider built-in)";
        for (int i = 1; i <= 16; i++)
        {
            labels[i] = $"Prompt Slot {i - 1}";
        }

        return labels;
    }
}
