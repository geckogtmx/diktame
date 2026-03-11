
using CommunityToolkit.Mvvm.ComponentModel;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using DiktaMe.Core.STT;
using Microsoft.UI.Dispatching;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class AIEngineSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;
    private readonly DispatcherQueue _dispatcher;
    private bool _isLoading;
    private CancellationTokenSource? _downloadCts;

    [ObservableProperty]
    private int _sttModeIndex; // 0 = Cloud, 1 = Local

    [ObservableProperty]
    private int _llmModeIndex; // 0 = Cloud, 1 = Local (Ollama), 2 = Skip

    [ObservableProperty]
    private string _capabilitySummary = "";

    // ── Whisper settings ────────────────────────────────────────────────────

    [ObservableProperty]
    private int _whisperModelIndex;

    [ObservableProperty]
    private bool _isWhisperSectionVisible;

    [ObservableProperty]
    private bool _isWhisperDownloading;

    [ObservableProperty]
    private int _whisperDownloadPercent;

    [ObservableProperty]
    private string _whisperDownloadStatus = "";

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

    [ObservableProperty]
    private bool _isDeepgramSectionVisible = true;

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
        _dispatcher = DispatcherQueue.GetForCurrentThread();
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

    public string[] WhisperModels { get; } =
    [
        "Tiny (~75 MB)",
        "Base (~142 MB)",
        "Small (~466 MB, recommended)",
        "Medium (~1.5 GB)",
        "Large (~3 GB)",
        "Turbo (~1.6 GB)",
    ];
    public string[] WhisperModelCodes { get; } = ["tiny", "base", "small", "medium", "large", "turbo"];

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

        // Whisper settings
        WhisperModelIndex = Array.IndexOf(WhisperModelCodes, s.WhisperModel) is var wi and >= 0 ? wi : 2; // default: small (index 2)
        IsWhisperSectionVisible = SttModeIndex == 1;
        IsDeepgramSectionVisible = SttModeIndex == 0;

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

    // ── Mode change handlers ────────────────────────────────────────────────

    partial void OnSttModeIndexChanged(int value)
    {
        if (_isLoading)
        {
            return;
        }

        string sttProvider = value == 1 ? "whisper" : "deepgram";
        IsWhisperSectionVisible = value == 1;
        IsDeepgramSectionVisible = value == 0;

        // Update all ModeProfiles
        var profiles = new Dictionary<string, ModeSettings>(_settings.Current.ModeProfiles);
        string[] modes = ["dictate", "refine", "ask", "translate", "note", "chat"];
        foreach (var mode in modes)
        {
            for (int p = 0; p < 2; p++)
            {
                string key = $"{mode}_{p}";
                var existing = profiles.TryGetValue(key, out var ms) ? ms : new ModeSettings();
                profiles[key] = existing with { SttProvider = sttProvider };
            }
        }

        var updated = _settings.Current with { ModeProfiles = profiles };
        UpdateCapabilitySummary(updated);
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save STT mode change");
            }
        }, TaskScheduler.Default);
    }

    partial void OnLlmModeIndexChanged(int value)
    {
        if (_isLoading)
        {
            return;
        }

        string llmProvider = value switch
        {
            1 => "ollama",
            2 => "none",
            _ => "gemini",
        };
        bool useLlm = !string.Equals(llmProvider, "none", StringComparison.Ordinal);

        // LLM choice determines ActiveProfileName
        string profileName = value == 1 ? "Local" : "Cloud";

        // Update all ModeProfiles
        var profiles = new Dictionary<string, ModeSettings>(_settings.Current.ModeProfiles);
        string[] modes = ["dictate", "refine", "ask", "translate", "note", "chat"];
        foreach (var mode in modes)
        {
            for (int p = 0; p < 2; p++)
            {
                string key = $"{mode}_{p}";
                var existing = profiles.TryGetValue(key, out var ms) ? ms : new ModeSettings();
                profiles[key] = existing with { LlmProvider = llmProvider, UseLlm = useLlm };
            }
        }

        var updated = _settings.Current with
        {
            ModeProfiles = profiles,
            ActiveProfileName = profileName,
        };
        UpdateCapabilitySummary(updated);
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save LLM mode change");
            }
        }, TaskScheduler.Default);
    }

    private void UpdateCapabilitySummary(AppSettings s)
    {
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
        CapabilitySummary = $"STT: {sttLabel}  |  LLM: {llmLabel}";
    }

    // ── Whisper change handler ──────────────────────────────────────────────

    partial void OnWhisperModelIndexChanged(int value)
    {
        if (_isLoading)
        {
            return;
        }

        string model = value >= 0 && value < WhisperModelCodes.Length
            ? WhisperModelCodes[value] : "small";

        var updated = _settings.Current with { WhisperModel = model };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save Whisper model setting");
            }
        }, TaskScheduler.Default);

        // Check if model is downloaded — if not, trigger download
        _ = EnsureWhisperModelDownloadedAsync(model);
    }

    private async Task EnsureWhisperModelDownloadedAsync(string modelCode)
    {
        // Cancel any in-flight download
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
        _downloadCts = new CancellationTokenSource();
        var ct = _downloadCts.Token;

        // Create a temporary WhisperProvider for the target model to check + download
        using var whisper = new WhisperProvider(modelCode);
        if (whisper.IsModelDownloaded)
        {
            IsWhisperDownloading = false;
            return;
        }

        IsWhisperDownloading = true;
        WhisperDownloadPercent = 0;
        WhisperDownloadStatus = _loc.GetFormatted("Settings_Whisper_Downloading", 0);

        whisper.DownloadProgress += (_, e) =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                WhisperDownloadPercent = e.Percent;
                WhisperDownloadStatus = _loc.GetFormatted("Settings_Whisper_Downloading", e.Percent);
            });
        };

        try
        {
            await whisper.DownloadModelAsync(ct);
            _dispatcher.TryEnqueue(() =>
            {
                WhisperDownloadPercent = 100;
                WhisperDownloadStatus = _loc.GetString("Settings_Whisper_DownloadComplete");
            });
            Log.Information("Settings: Whisper model '{Model}' downloaded", modelCode);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Settings: Whisper model download cancelled");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Settings: Whisper model download failed");
            _dispatcher.TryEnqueue(() =>
            {
                WhisperDownloadStatus = _loc.GetFormatted("Settings_Whisper_DownloadFailed", ex.Message);
            });
        }
        finally
        {
            _dispatcher.TryEnqueue(() => IsWhisperDownloading = false);
        }
    }

    // ── Deepgram change handlers ────────────────────────────────────────────

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
