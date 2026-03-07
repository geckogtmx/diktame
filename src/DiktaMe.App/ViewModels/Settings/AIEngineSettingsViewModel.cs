
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class AIEngineSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;
    private bool _isLoading;

    [ObservableProperty]
    private int _sttModeIndex; // 0 = Cloud, 1 = Local

    [ObservableProperty]
    private int _llmModeIndex; // 0 = Cloud, 1 = Local (Ollama), 2 = Skip

    [ObservableProperty]
    private string _capabilitySummary = "";

    // ── Deepgram settings ────────────────────────────────────────────────────

    [ObservableProperty]
    private int _deepgramModelIndex; // 0 = nova-3, 1 = nova-2

    [ObservableProperty]
    private bool _deepgramPunctuate = true;

    [ObservableProperty]
    private bool _deepgramDictation = true;

    [ObservableProperty]
    private bool _deepgramSmartFormat;

    [ObservableProperty]
    private string _deepgramReplacements = "";

    [ObservableProperty]
    private bool _deepgramStreaming;

    /// <summary>
    /// Dictation toggle is disabled when Punctuate is off and SmartFormat is off
    /// (dictation requires punctuation to function).
    /// </summary>
    [ObservableProperty]
    private bool _isDictationEnabled = true;

    public AIEngineSettingsViewModel(SettingsManager settings, LocalizationService loc)
    {
        _settings = settings;
        _loc = loc;
        LoadFromSettings();
    }

    public string[] SttModes => [
        _loc.GetString("Settings_AIEngine_STT_Cloud"),
        _loc.GetString("Settings_AIEngine_STT_Local"),
    ];
    public string[] LlmModes => [
        _loc.GetString("Settings_AIEngine_LLM_Cloud"),
        _loc.GetString("Settings_AIEngine_LLM_Local"),
        _loc.GetString("Settings_AIEngine_LLM_Skip"),
    ];
    public string[] DeepgramModels => [
        _loc.GetString("Settings_AIEngine_Deepgram_Nova3"),
        _loc.GetString("Settings_AIEngine_Deepgram_Nova2"),
    ];
    public string[] DeepgramModelCodes { get; } = ["nova-3", "nova-2"];

    private void LoadFromSettings()
    {
        _isLoading = true;

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

        SttModeIndex = string.Equals(defaultMode.SttProvider, "whisper", StringComparison.Ordinal) ? 1 : 0;
        LlmModeIndex = defaultMode.LlmProvider switch
        {
            "ollama" => 1,
            "none" => 2,
            _ => 0,
        };

        CapabilitySummary = $"STT: {sttLabel}  |  LLM: {llmLabel}";

        // Deepgram settings
        var dg = s.Deepgram;
        DeepgramModelIndex = Array.IndexOf(DeepgramModelCodes, dg.Model) is var mi and >= 0 ? mi : 0;
        DeepgramPunctuate = dg.Punctuate;
        DeepgramDictation = dg.Dictation;
        DeepgramSmartFormat = dg.SmartFormat;
        DeepgramReplacements = string.Join("\n", dg.Replacements);
        DeepgramStreaming = s.General.StreamingEnabled;
        IsDictationEnabled = dg.Punctuate || dg.SmartFormat;

        _isLoading = false;
    }

    // ── Change handlers ──────────────────────────────────────────────────────

    partial void OnDeepgramModelIndexChanged(int value) => SaveDeepgram();
    partial void OnDeepgramSmartFormatChanged(bool value)
    {
        UpdateDictationEnabled();
        SaveDeepgram();
    }

    partial void OnDeepgramPunctuateChanged(bool value)
    {
        UpdateDictationEnabled();
        SaveDeepgram();
    }

    partial void OnDeepgramDictationChanged(bool value) => SaveDeepgram();
    partial void OnDeepgramReplacementsChanged(string value) => SaveDeepgram();

    partial void OnDeepgramStreamingChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        var updated = _settings.Current with
        {
            General = _settings.Current.General with { StreamingEnabled = value }
        };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save streaming setting");
            }
        }, TaskScheduler.Default);
    }

    private void UpdateDictationEnabled()
    {
        IsDictationEnabled = DeepgramPunctuate || DeepgramSmartFormat;
        if (!IsDictationEnabled)
        {
            DeepgramDictation = false;
        }
    }

    private void SaveDeepgram()
    {
        if (_isLoading)
        {
            return;
        }

        // Parse replacements: one "find:replace" per line, skip blanks
        var replacements = DeepgramReplacements
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains(':', StringComparison.Ordinal))
            .ToList();

        var updated = _settings.Current with
        {
            Deepgram = new DeepgramSettings
            {
                Model = DeepgramModelIndex >= 0 && DeepgramModelIndex < DeepgramModelCodes.Length
                    ? DeepgramModelCodes[DeepgramModelIndex] : "nova-3",
                Punctuate = DeepgramPunctuate,
                Dictation = DeepgramDictation,
                SmartFormat = DeepgramSmartFormat,
                Replacements = replacements,
            },
        };

        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save Deepgram settings");
            }
        }, TaskScheduler.Default);
    }
}
