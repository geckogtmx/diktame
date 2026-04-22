
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Security;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class ApiKeysSettingsViewModel : ObservableObject
{
    private readonly SecureStorage _storage;
    private readonly ApiKeyTester _tester;
    private readonly LocalizationService _loc;

    // ── OpenAI ──────────────────────────────────────────────────────────────

    [ObservableProperty] private string _openAiKey = "";
    [ObservableProperty] private string _openAiStatus = "";
    [ObservableProperty] private bool _openAiHasKey;
    [ObservableProperty] private bool _openAiTesting;

    // ── Anthropic ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _anthropicKey = "";
    [ObservableProperty] private string _anthropicStatus = "";
    [ObservableProperty] private bool _anthropicHasKey;
    [ObservableProperty] private bool _anthropicTesting;

    // ── Gemini ──────────────────────────────────────────────────────────────

    [ObservableProperty] private string _geminiKey = "";
    [ObservableProperty] private string _geminiStatus = "";
    [ObservableProperty] private bool _geminiHasKey;
    [ObservableProperty] private bool _geminiTesting;

    // ── Deepgram ────────────────────────────────────────────────────────────

    [ObservableProperty] private string _deepgramKey = "";
    [ObservableProperty] private string _deepgramStatus = "";
    [ObservableProperty] private bool _deepgramHasKey;
    [ObservableProperty] private bool _deepgramTesting;

    // ── Inworld ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _inworldKey = "";
    [ObservableProperty] private string _inworldStatus = "";
    [ObservableProperty] private bool _inworldHasKey;
    [ObservableProperty] private bool _inworldTesting;

    // ── OpenRouter ──────────────────────────────────────────────────────────

    [ObservableProperty] private string _openRouterKey = "";
    [ObservableProperty] private string _openRouterStatus = "";
    [ObservableProperty] private bool _openRouterHasKey;
    [ObservableProperty] private bool _openRouterTesting;

    // ── Requesty ────────────────────────────────────────────────────────────

    [ObservableProperty] private string _requestyKey = "";
    [ObservableProperty] private string _requestyStatus = "";
    [ObservableProperty] private bool _requestyHasKey;
    [ObservableProperty] private bool _requestyTesting;

    public ApiKeysSettingsViewModel(SecureStorage storage, ApiKeyTester tester, LocalizationService loc)
    {
        _storage = storage;
        _tester = tester;
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

    [RelayCommand] private void SaveInworldKey() => SaveKey("inworld", InworldKey, ApiKeyValidator.IsValidGeneric, v => { InworldStatus = v; InworldHasKey = string.Equals(v, _loc.GetString("Settings_ApiKeys_Status_Saved"), StringComparison.Ordinal); });
    [RelayCommand] private void DeleteInworldKey() => DeleteKey("inworld", v => { InworldStatus = v; InworldHasKey = false; InworldKey = ""; });

    [RelayCommand] private void SaveOpenRouterKey() => SaveKey("openrouter", OpenRouterKey, ApiKeyValidator.IsValidGeneric, v => { OpenRouterStatus = v; OpenRouterHasKey = string.Equals(v, _loc.GetString("Settings_ApiKeys_Status_Saved"), StringComparison.Ordinal); });
    [RelayCommand] private void DeleteOpenRouterKey() => DeleteKey("openrouter", v => { OpenRouterStatus = v; OpenRouterHasKey = false; OpenRouterKey = ""; });

    [RelayCommand] private void SaveRequestyKey() => SaveKey("requesty", RequestyKey, ApiKeyValidator.IsValidGeneric, v => { RequestyStatus = v; RequestyHasKey = string.Equals(v, _loc.GetString("Settings_ApiKeys_Status_Saved"), StringComparison.Ordinal); });
    [RelayCommand] private void DeleteRequestyKey() => DeleteKey("requesty", v => { RequestyStatus = v; RequestyHasKey = false; RequestyKey = ""; });

    // ── Test Connection commands ────────────────────────────────────────────

    [RelayCommand]
    private Task TestOpenAiKeyAsync() => RunTestAsync("openai", OpenAiKey,
        v => OpenAiTesting = v, s => OpenAiStatus = s);

    [RelayCommand]
    private Task TestAnthropicKeyAsync() => RunTestAsync("anthropic", AnthropicKey,
        v => AnthropicTesting = v, s => AnthropicStatus = s);

    [RelayCommand]
    private Task TestGeminiKeyAsync() => RunTestAsync("gemini", GeminiKey,
        v => GeminiTesting = v, s => GeminiStatus = s);

    [RelayCommand]
    private Task TestDeepgramKeyAsync() => RunTestAsync("deepgram", DeepgramKey,
        v => DeepgramTesting = v, s => DeepgramStatus = s);

    [RelayCommand]
    private Task TestInworldKeyAsync() => RunTestAsync("inworld", InworldKey,
        v => InworldTesting = v, s => InworldStatus = s);

    [RelayCommand]
    private Task TestOpenRouterKeyAsync() => RunTestAsync("openrouter", OpenRouterKey,
        v => OpenRouterTesting = v, s => OpenRouterStatus = s);

    [RelayCommand]
    private Task TestRequestyKeyAsync() => RunTestAsync("requesty", RequestyKey,
        v => RequestyTesting = v, s => RequestyStatus = s);

    private async Task RunTestAsync(string provider, string typedKey, Action<bool> setTesting, Action<string> setStatus)
    {
        setTesting(true);
        setStatus(_loc.GetString("Settings_ApiKeys_Status_Testing"));
        try
        {
            var result = await _tester.TestAsync(provider, typedKey).ConfigureAwait(true);
            setStatus(result.Message);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Test Connection failed for {Provider}", provider);
            setStatus(_loc.GetString("Settings_ApiKeys_Status_TestUnexpected"));
        }
        finally
        {
            setTesting(false);
        }
    }

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

        InworldHasKey = _storage.RetrieveKey("inworld") is not null;
        InworldStatus = InworldHasKey ? _loc.GetString("Settings_ApiKeys_Status_Saved") : _loc.GetString("Settings_ApiKeys_Status_NotSet");

        OpenRouterHasKey = _storage.RetrieveKey("openrouter") is not null;
        OpenRouterStatus = OpenRouterHasKey ? _loc.GetString("Settings_ApiKeys_Status_Saved") : _loc.GetString("Settings_ApiKeys_Status_NotSet");

        RequestyHasKey = _storage.RetrieveKey("requesty") is not null;
        RequestyStatus = RequestyHasKey ? _loc.GetString("Settings_ApiKeys_Status_Saved") : _loc.GetString("Settings_ApiKeys_Status_NotSet");
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
