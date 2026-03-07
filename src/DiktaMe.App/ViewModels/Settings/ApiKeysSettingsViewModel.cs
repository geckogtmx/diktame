
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class ApiKeysSettingsViewModel : ObservableObject
{
    private readonly SecureStorage _storage;
    private readonly LocalizationService _loc;

    // ── OpenAI ──────────────────────────────────────────────────────────────

    [ObservableProperty] private string _openAiKey = "";
    [ObservableProperty] private string _openAiStatus = "";
    [ObservableProperty] private bool _openAiHasKey;

    // ── Anthropic ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _anthropicKey = "";
    [ObservableProperty] private string _anthropicStatus = "";
    [ObservableProperty] private bool _anthropicHasKey;

    // ── Gemini ──────────────────────────────────────────────────────────────

    [ObservableProperty] private string _geminiKey = "";
    [ObservableProperty] private string _geminiStatus = "";
    [ObservableProperty] private bool _geminiHasKey;

    // ── Deepgram ────────────────────────────────────────────────────────────

    [ObservableProperty] private string _deepgramKey = "";
    [ObservableProperty] private string _deepgramStatus = "";
    [ObservableProperty] private bool _deepgramHasKey;

    public ApiKeysSettingsViewModel(SecureStorage storage, LocalizationService loc)
    {
        _storage = storage;
        _loc = loc;
        RefreshKeyStatus();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand] private void SaveOpenAiKey() => SaveKey("openai", OpenAiKey, ApiKeyValidator.IsValidOpenAI, v => { OpenAiStatus = v; OpenAiHasKey = string.Equals(v, _loc.GetString("Settings_ApiKeys_Status_Saved"), StringComparison.Ordinal); });
    [RelayCommand] private void DeleteOpenAiKey() => DeleteKey("openai", v => { OpenAiStatus = v; OpenAiHasKey = false; OpenAiKey = ""; });

    [RelayCommand] private void SaveAnthropicKey() => SaveKey("anthropic", AnthropicKey, ApiKeyValidator.IsValidAnthropic, v => { AnthropicStatus = v; AnthropicHasKey = string.Equals(v, _loc.GetString("Settings_ApiKeys_Status_Saved"), StringComparison.Ordinal); });
    [RelayCommand] private void DeleteAnthropicKey() => DeleteKey("anthropic", v => { AnthropicStatus = v; AnthropicHasKey = false; AnthropicKey = ""; });

    [RelayCommand] private void SaveGeminiKey() => SaveKey("gemini", GeminiKey, ApiKeyValidator.IsValidGemini, v => { GeminiStatus = v; GeminiHasKey = string.Equals(v, _loc.GetString("Settings_ApiKeys_Status_Saved"), StringComparison.Ordinal); });
    [RelayCommand] private void DeleteGeminiKey() => DeleteKey("gemini", v => { GeminiStatus = v; GeminiHasKey = false; GeminiKey = ""; });

    [RelayCommand] private void SaveDeepgramKey() => SaveKey("deepgram", DeepgramKey, ApiKeyValidator.IsValidDeepgram, v => { DeepgramStatus = v; DeepgramHasKey = string.Equals(v, _loc.GetString("Settings_ApiKeys_Status_Saved"), StringComparison.Ordinal); });
    [RelayCommand] private void DeleteDeepgramKey() => DeleteKey("deepgram", v => { DeepgramStatus = v; DeepgramHasKey = false; DeepgramKey = ""; });

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RefreshKeyStatus()
    {
        OpenAiHasKey = _storage.RetrieveKey("openai") is not null;
        OpenAiStatus = OpenAiHasKey ? _loc.GetString("Settings_ApiKeys_Status_Saved") : _loc.GetString("Settings_ApiKeys_Status_NotSet");

        AnthropicHasKey = _storage.RetrieveKey("anthropic") is not null;
        AnthropicStatus = AnthropicHasKey ? _loc.GetString("Settings_ApiKeys_Status_Saved") : _loc.GetString("Settings_ApiKeys_Status_NotSet");

        GeminiHasKey = _storage.RetrieveKey("gemini") is not null;
        GeminiStatus = GeminiHasKey ? _loc.GetString("Settings_ApiKeys_Status_Saved") : _loc.GetString("Settings_ApiKeys_Status_NotSet");

        DeepgramHasKey = _storage.RetrieveKey("deepgram") is not null;
        DeepgramStatus = DeepgramHasKey ? _loc.GetString("Settings_ApiKeys_Status_Saved") : _loc.GetString("Settings_ApiKeys_Status_NotSet");
    }

    private void SaveKey(string provider, string key, Func<string?, bool> validator, Action<string> setStatus)
    {
        if (!validator(key))
        {
            setStatus(_loc.GetString("Settings_ApiKeys_Status_InvalidFormat"));
            return;
        }

        try
        {
            _storage.StoreKey(provider, key);
            setStatus(_loc.GetString("Settings_ApiKeys_Status_Saved"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save {Provider} key", provider);
            setStatus(_loc.GetString("Settings_ApiKeys_Status_SaveFailed"));
        }
    }

    private void DeleteKey(string provider, Action<string> setStatus)
    {
        try
        {
            _storage.DeleteKey(provider);
            setStatus(_loc.GetString("Settings_ApiKeys_Status_NotSet"));
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete {Provider} key", provider);
            setStatus(_loc.GetString("Settings_ApiKeys_Status_DeleteFailed"));
        }
    }
}
