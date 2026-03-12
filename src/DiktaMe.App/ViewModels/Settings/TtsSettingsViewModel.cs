
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Audio;
using DiktaMe.Core.Config;
using DiktaMe.Core.TTS;
using Microsoft.UI.Dispatching;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class TtsSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly LocalizationService _loc;
    private readonly ITTSProviderFactory _ttsFactory;
    private readonly ITtsPlayerService _ttsPlayer;
    private readonly AudioDucker _ducker;
    private readonly DispatcherQueue? _dispatcher;
    private bool _isLoading;

    // ── Provider / Voice data ────────────────────────────────────────────────

    public static readonly string[] ProviderKeys = ["kokoro", "deepgram", "inworld", "openai"];

    public string[] ProviderLabels { get; }

    public static readonly string[] KokoroVariantKeys = ["int8", "fp16", "fp32"];
    public string[] KokoroVariantLabels { get; } = ["int8 (88 MB)", "fp16 (169 MB)", "fp32 (310 MB)"];

    private static readonly Dictionary<string, string[]> VoiceLists = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kokoro"] =
        [
            "af_bella", "af_nicole", "af_sarah", "af_sky",
            "am_adam", "am_michael",
            "bf_emma", "bf_isabella",
            "bm_george", "bm_lewis",
        ],
        ["deepgram"] =
        [
            "aura-asteria-en", "aura-luna-en", "aura-stella-en",
            "aura-hera-en", "aura-orion-en", "aura-arcas-en",
            "aura-perseus-en", "aura-angus-en", "aura-orpheus-en",
            "aura-helios-en", "aura-zeus-en",
        ],
        ["inworld"] =
        [
            "Timothy", "Deborah", "Ashley", "Brian",
            "Catherine", "David", "Emma", "George",
        ],
        ["openai"] =
        [
            "alloy", "ash", "ballad", "coral", "echo",
            "fable", "fern", "nova", "onyx", "sage", "shimmer",
        ],
    };

    // ── Provider ─────────────────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedProviderIndex;

    [ObservableProperty]
    private ObservableCollection<string> _availableVoices = new();

    [ObservableProperty]
    private int _selectedVoiceIndex = -1;

    // ── Playback parameters ──────────────────────────────────────────────────

    [ObservableProperty]
    private double _speed = 1.0;

    [ObservableProperty]
    private double _volumePercent = 80;

    [ObservableProperty]
    private int _maxSpeechWords = 500;

    // ── Per-mode toggles ─────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _speakAskResponses;

    [ObservableProperty]
    private bool _speakChatResponses;

    [ObservableProperty]
    private bool _speakTranslations;

    [ObservableProperty]
    private bool _speakNotifications;

    [ObservableProperty]
    private bool _duckDuringPlayback = true;

    // ── Kokoro model ─────────────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedKokoroVariantIndex;

    [ObservableProperty]
    private bool _isKokoroModelDownloaded;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatusText = "";

    // ── Test voice ───────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isTestPlaying;

    [ObservableProperty]
    private string _testStatusText = "";

    // ── Computed ──────────────────────────────────────────────────────────────

    public bool IsKokoroSelected => SelectedProviderIndex == 0;

    public string ReadSelectionHotkey => _settings.Current.Hotkeys.ReadSelection;

    // ── Constructor ──────────────────────────────────────────────────────────

    public TtsSettingsViewModel(
        SettingsManager settings,
        LocalizationService loc,
        ITTSProviderFactory ttsFactory,
        ITtsPlayerService ttsPlayer,
        AudioDucker ducker)
    {
        _settings = settings;
        _loc = loc;
        _ttsFactory = ttsFactory;
        _ttsPlayer = ttsPlayer;
        _ducker = ducker;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        ProviderLabels =
        [
            _loc.GetString("Settings_Tts_Provider_Kokoro"),
            _loc.GetString("Settings_Tts_Provider_Deepgram"),
            _loc.GetString("Settings_Tts_Provider_Inworld"),
            _loc.GetString("Settings_Tts_Provider_OpenAI"),
        ];

        try
        {
            LoadFromSettings();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TtsSettingsViewModel: LoadFromSettings failed — using defaults");
        }
    }

    // ── Load from settings ───────────────────────────────────────────────────

    private void LoadFromSettings()
    {
        _isLoading = true;
        var tts = _settings.Current.Tts;

        SelectedProviderIndex = Array.IndexOf(ProviderKeys, tts.Provider) is var i and >= 0 ? i : 0;
        Speed = tts.Speed;
        VolumePercent = tts.VolumePercent;
        MaxSpeechWords = tts.MaxSpeechWords;
        SpeakAskResponses = tts.SpeakAskResponses;
        SpeakChatResponses = tts.SpeakChatResponses;
        SpeakTranslations = tts.SpeakTranslations;
        SpeakNotifications = tts.SpeakNotifications;
        DuckDuringPlayback = tts.DuckDuringPlayback;
        SelectedKokoroVariantIndex = Array.IndexOf(KokoroVariantKeys, tts.KokoroModelVariant) is var k and >= 0 ? k : 0;

        RefreshVoiceList();
        SelectVoice(tts.VoiceId);
        RefreshKokoroModelState();

        _isLoading = false;
    }

    // ── Voice list management ────────────────────────────────────────────────

    private void RefreshVoiceList()
    {
        string providerKey = SelectedProviderIndex >= 0 && SelectedProviderIndex < ProviderKeys.Length
            ? ProviderKeys[SelectedProviderIndex] : "kokoro";

        AvailableVoices.Clear();
        if (VoiceLists.TryGetValue(providerKey, out var voices))
        {
            foreach (var v in voices)
            {
                AvailableVoices.Add(v);
            }
        }
    }

    private void SelectVoice(string voiceId)
    {
        if (string.IsNullOrEmpty(voiceId))
        {
            SelectedVoiceIndex = AvailableVoices.Count > 0 ? 0 : -1;
            return;
        }

        for (int i = 0; i < AvailableVoices.Count; i++)
        {
            if (string.Equals(AvailableVoices[i], voiceId, StringComparison.OrdinalIgnoreCase))
            {
                SelectedVoiceIndex = i;
                return;
            }
        }

        SelectedVoiceIndex = AvailableVoices.Count > 0 ? 0 : -1;
    }

    private string GetSelectedVoiceId()
    {
        return SelectedVoiceIndex >= 0 && SelectedVoiceIndex < AvailableVoices.Count
            ? AvailableVoices[SelectedVoiceIndex] : string.Empty;
    }

    // ── Kokoro model state ───────────────────────────────────────────────────

    private void RefreshKokoroModelState()
    {
        try
        {
            string variant = SelectedKokoroVariantIndex >= 0 && SelectedKokoroVariantIndex < KokoroVariantKeys.Length
                ? KokoroVariantKeys[SelectedKokoroVariantIndex] : "int8";

            var manager = new KokoroModelManager(variant);
            IsKokoroModelDownloaded = manager.IsModelDownloaded;

            DownloadStatusText = IsKokoroModelDownloaded
                ? _loc.GetString("Settings_Tts_Kokoro_Status_Downloaded")
                : _loc.GetString("Settings_Tts_Kokoro_Status_NotDownloaded");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to refresh Kokoro model state");
            IsKokoroModelDownloaded = false;
            DownloadStatusText = _loc.GetString("Settings_Tts_Kokoro_Status_NotDownloaded");
        }
    }

    // ── Change handlers ──────────────────────────────────────────────────────

    partial void OnSelectedProviderIndexChanged(int value)
    {
        if (!_isLoading)
        {
            RefreshVoiceList();
            SelectedVoiceIndex = AvailableVoices.Count > 0 ? 0 : -1;
        }

        OnPropertyChanged(nameof(IsKokoroSelected));
        Save();
    }

    partial void OnSelectedVoiceIndexChanged(int value) => Save();
    partial void OnSpeedChanged(double value) => Save();
    partial void OnVolumePercentChanged(double value) => Save();
    partial void OnMaxSpeechWordsChanged(int value) => Save();
    partial void OnSpeakAskResponsesChanged(bool value) => Save();
    partial void OnSpeakChatResponsesChanged(bool value) => Save();
    partial void OnSpeakTranslationsChanged(bool value) => Save();
    partial void OnSpeakNotificationsChanged(bool value) => Save();
    partial void OnDuckDuringPlaybackChanged(bool value) => Save();

    partial void OnSelectedKokoroVariantIndexChanged(int value)
    {
        RefreshKokoroModelState();
        Save();
    }

    // ── Save ─────────────────────────────────────────────────────────────────

    private void Save()
    {
        if (_isLoading)
        {
            return;
        }

        string provider = SelectedProviderIndex >= 0 && SelectedProviderIndex < ProviderKeys.Length
            ? ProviderKeys[SelectedProviderIndex] : "kokoro";
        string variant = SelectedKokoroVariantIndex >= 0 && SelectedKokoroVariantIndex < KokoroVariantKeys.Length
            ? KokoroVariantKeys[SelectedKokoroVariantIndex] : "int8";

        var updated = _settings.Current with
        {
            Tts = new TtsSettings
            {
                Enabled = _settings.Current.Tts.Enabled,
                Provider = provider,
                VoiceId = GetSelectedVoiceId(),
                Speed = Speed,
                VolumePercent = (int)VolumePercent,
                MaxSpeechWords = MaxSpeechWords,
                SpeakAskResponses = SpeakAskResponses,
                SpeakChatResponses = SpeakChatResponses,
                SpeakTranslations = SpeakTranslations,
                SpeakNotifications = SpeakNotifications,
                DuckDuringPlayback = DuckDuringPlayback,
                KokoroModelVariant = variant,
            },
        };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save TTS settings");
            }
        }, TaskScheduler.Default);
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task TestVoiceAsync()
    {
        if (IsTestPlaying)
        {
            return;
        }

        IsTestPlaying = true;
        TestStatusText = _loc.GetString("Settings_Tts_TestVoice_Playing");

        try
        {
            string provider = SelectedProviderIndex >= 0 && SelectedProviderIndex < ProviderKeys.Length
                ? ProviderKeys[SelectedProviderIndex] : "kokoro";
            string variant = SelectedKokoroVariantIndex >= 0 && SelectedKokoroVariantIndex < KokoroVariantKeys.Length
                ? KokoroVariantKeys[SelectedKokoroVariantIndex] : "int8";
            string voiceId = GetSelectedVoiceId();

            // Pre-flight: Kokoro needs a downloaded model
            if (string.Equals(provider, "kokoro", StringComparison.Ordinal) && !IsKokoroModelDownloaded)
            {
                TestStatusText = _loc.GetString("Settings_Tts_TestVoice_ModelNotDownloaded");
                return;
            }

            Log.Information("TTS test: provider={Provider}, variant={Variant}, voice={Voice}", provider, variant, voiceId);
            ITTSProvider ttsProvider = _ttsFactory.CreateProvider(provider, variant);
            string sampleText = _loc.GetString("Settings_Tts_TestVoice_SampleText");

            TtsResult result = await ttsProvider.SynthesizeAsync(sampleText, voiceId);

            if (!result.IsSuccess)
            {
                TestStatusText = _loc.GetString("Settings_Tts_TestVoice_Error");
                return;
            }

            bool shouldDuck = DuckDuringPlayback && _settings.Current.AudioDucking.Enabled;
            if (shouldDuck)
            {
                _ducker.IsEnabled = true;
                _ducker.DuckLevel = _settings.Current.AudioDucking.DuckLevelPercent / 100f;
                _ducker.Duck();
            }

            try
            {
                _ttsPlayer.Volume = (float)(VolumePercent / 100.0);
                await _ttsPlayer.PlayAsync(result.AudioData, result.SampleRate);
                TestStatusText = "";
            }
            finally
            {
                if (shouldDuck)
                {
                    _ducker.Restore();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "TTS test voice failed");
            TestStatusText = _loc.GetString("Settings_Tts_TestVoice_Error");
        }
        finally
        {
            IsTestPlaying = false;
        }
    }

    [RelayCommand]
    private async Task DownloadKokoroModelAsync()
    {
        if (IsDownloading)
        {
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;
        DownloadStatusText = _loc.GetString("Settings_Tts_Kokoro_Downloading");

        string variant = SelectedKokoroVariantIndex >= 0 && SelectedKokoroVariantIndex < KokoroVariantKeys.Length
            ? KokoroVariantKeys[SelectedKokoroVariantIndex] : "int8";

        var manager = new KokoroModelManager(variant);
        manager.DownloadProgress += (_, e) =>
        {
            _dispatcher?.TryEnqueue(() =>
            {
                DownloadProgress = e.Percent;
                DownloadStatusText = $"{e.Percent}% ({e.BytesRead / 1_048_576} / {e.TotalBytes / 1_048_576} MB)";
            });
        };

        try
        {
            await manager.DownloadModelAsync();

            _dispatcher?.TryEnqueue(() =>
            {
                DownloadStatusText = _loc.GetString("Settings_Tts_Kokoro_DownloadComplete");
                IsKokoroModelDownloaded = true;
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Kokoro model download failed ({ExType}: {ExMsg})", ex.GetType().Name, ex.Message);
            _dispatcher?.TryEnqueue(() =>
            {
                DownloadStatusText = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    _loc.GetString("Settings_Tts_Kokoro_DownloadFailed"),
                    ex.Message);
            });
        }
        finally
        {
            _dispatcher?.TryEnqueue(() =>
            {
                IsDownloading = false;
            });
        }
    }
}
