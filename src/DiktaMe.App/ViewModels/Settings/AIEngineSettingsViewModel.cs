
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using DiktaMe.Core.STT;
using Microsoft.UI.Dispatching;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// Host ViewModel for the AI Engine settings page.
/// 6 sub-items: API Keys, Speech to Text, Language Model, Text to Speech, Chat, System Monitor.
/// </summary>
public sealed partial class AIEngineSettingsViewModel : ObservableObject
{
    public ApiKeysSettingsViewModel ApiKeys { get; }
    public OllamaSettingsViewModel Ollama { get; }
    public TtsSettingsViewModel Tts { get; }
    public ModesSettingsViewModel Pipelines { get; }
    public CloudLlmSettingsViewModel CloudLlm { get; }

    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;
    private readonly DispatcherQueue _dispatcher;
    private bool _isLoading;
    private CancellationTokenSource? _downloadCts;

    // ── Inner list ───────────────────────────────────────────────────────

    public ObservableCollection<ModeListItem> SubItems { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isApiKeysSelected;

    [ObservableProperty]
    private bool _isSttSelected;

    [ObservableProperty]
    private bool _isLlmSelected;

    [ObservableProperty]
    private bool _isTtsSelected;

    [ObservableProperty]
    private bool _isChatSelected;

    [ObservableProperty]
    private bool _isSystemMonitorSelected;

    // ── Cloud/Local tab selection (true = Cloud, false = Local) ──────────

    [ObservableProperty]
    private bool _isSttCloudTab = true;

    [ObservableProperty]
    private bool _isLlmCloudTab = true;

    [ObservableProperty]
    private bool _isTtsCloudTab = true;

    // ── Whisper settings ────────────────────────────────────────────────────

    [ObservableProperty]
    private int _whisperModelIndex;

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

    /// <summary>
    /// Dictation toggle is disabled when Punctuate is off and SmartFormat is off
    /// (dictation requires punctuation to function).
    /// </summary>
    [ObservableProperty]
    private bool _isDictationEnabled = true;

    // ── System Monitor computed properties ────────────────────────────────

    [ObservableProperty]
    private string _activeSttInfo = "";

    [ObservableProperty]
    private string _activeTtsInfo = "";

    public AIEngineSettingsViewModel(
        ApiKeysSettingsViewModel apiKeys,
        OllamaSettingsViewModel ollama,
        TtsSettingsViewModel tts,
        ModesSettingsViewModel pipelines,
        CloudLlmSettingsViewModel cloudLlm,
        SettingsManager settings,
        LocalizationService loc)
    {
        ApiKeys = apiKeys;
        Ollama = ollama;
        Tts = tts;
        Pipelines = pipelines;
        CloudLlm = cloudLlm;
        _settings = settings;
        _loc = loc;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        LoadSubItems();
        LoadFromSettings();

        if (SubItems.Count > 0)
        {
            SelectedIndex = 0;
        }
    }

    // ── Sub-item list ───────────────────────────────────────────────────

    private void LoadSubItems()
    {
        SubItems.Clear();
        SubItems.Add(new ModeListItem { Id = "apikeys", Title = _loc.GetString("Settings_AIEngine_Sub_ApiKeys"), IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "stt", Title = _loc.GetString("Settings_AIEngine_Sub_Stt"), IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "llm", Title = _loc.GetString("Settings_AIEngine_Sub_Llm"), IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "tts", Title = _loc.GetString("Settings_AIEngine_Sub_Tts"), IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "chat", Title = "Chat", IsDictationMode = false, IsSeparator = false });
        SubItems.Add(new ModeListItem { Id = "monitor", Title = _loc.GetString("Settings_AIEngine_Sub_Monitor"), IsDictationMode = false, IsSeparator = false });
    }

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < SubItems.Count;

        if (!HasSelection)
        {
            IsApiKeysSelected = false;
            IsSttSelected = false;
            IsLlmSelected = false;
            IsTtsSelected = false;
            IsChatSelected = false;
            IsSystemMonitorSelected = false;
            return;
        }

        string id = SubItems[value].Id;
        IsApiKeysSelected = id == "apikeys";
        IsSttSelected = id == "stt";
        IsLlmSelected = id == "llm";
        IsTtsSelected = id == "tts";
        IsChatSelected = id == "chat";
        IsSystemMonitorSelected = id == "monitor";

        // Sync Chat selection to the inner ModesSettingsViewModel
        if (IsChatSelected)
        {
            for (int i = 0; i < Pipelines.ModeItems.Count; i++)
            {
                if (string.Equals(Pipelines.ModeItems[i].Id, "chat", StringComparison.Ordinal))
                {
                    Pipelines.SelectedIndex = i;
                    break;
                }
            }
        }

        // Refresh System Monitor info when selected
        if (IsSystemMonitorSelected)
        {
            RefreshSystemMonitorInfo();
        }
    }

    // ── Cloud/Local tab commands ─────────────────────────────────────────

    [RelayCommand] private void SelectSttCloud() => IsSttCloudTab = true;
    [RelayCommand] private void SelectSttLocal() => IsSttCloudTab = false;
    [RelayCommand] private void SelectLlmCloud() => IsLlmCloudTab = true;
    [RelayCommand] private void SelectLlmLocal() => IsLlmCloudTab = false;
    [RelayCommand] private void SelectTtsCloud() => IsTtsCloudTab = true;
    [RelayCommand] private void SelectTtsLocal() => IsTtsCloudTab = false;

    partial void OnIsTtsCloudTabChanged(bool value)
    {
        // Sync active provider with selected tab
        if (!value)
        {
            Tts.SelectedProviderIndex = 0; // kokoro (local)
        }
        else
        {
            Tts.SelectedProviderIndex = Tts.SelectedCloudProviderIndex + 1;
        }
    }

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

        // Whisper settings
        WhisperModelIndex = Array.IndexOf(WhisperModelCodes, s.WhisperModel) is var wi and >= 0 ? wi : 2; // default: small (index 2)

        // Deepgram settings
        var dg = s.Deepgram;
        DeepgramModelIndex = Array.IndexOf(DeepgramModelCodes, dg.Model) is var mi and >= 0 ? mi : 0;
        DeepgramPunctuate = dg.Punctuate;
        DeepgramDictation = dg.Dictation;
        DeepgramSmartFormat = dg.SmartFormat;
        DeepgramReplacements = string.Join("\n", dg.Replacements);
        DeepgramStreaming = s.General.StreamingEnabled;
        IsDictationEnabled = dg.Punctuate || dg.SmartFormat;

        // System Monitor info
        RefreshSystemMonitorInfo();

        _isLoading = false;
    }

    // ── System Monitor ────────────────────────────────────────────────────

    private void RefreshSystemMonitorInfo()
    {
        var s = _settings.Current;
        var defaultMode = s.ModeProfiles.GetValueOrDefault("dictate_0", new ModeSettings());

        ActiveSttInfo = defaultMode.SttProvider switch
        {
            "whisper" => $"Whisper ({s.WhisperModel})",
            "deepgram" => $"Deepgram ({s.Deepgram.Model})",
            "gemini-audio" => "Gemini Audio",
            _ => defaultMode.SttProvider,
        };

        var ttsProvider = s.Tts.Provider;
        ActiveTtsInfo = ttsProvider switch
        {
            "kokoro" => $"Kokoro ({s.Tts.VoiceId})",
            "sapi" => $"Windows SAPI ({s.Tts.VoiceId})",
            "disabled" or "" => "Disabled",
            _ => ttsProvider,
        };
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
