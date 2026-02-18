namespace DiktaMe.App.ViewModels.Settings;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Security;
using Serilog;

public sealed partial class ApiKeysSettingsViewModel : ObservableObject
{
    private readonly SecureStorage _storage;

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

    public ApiKeysSettingsViewModel(SecureStorage storage)
    {
        _storage = storage;
        RefreshKeyStatus();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    [RelayCommand] private void SaveOpenAiKey() => SaveKey("openai", OpenAiKey, ApiKeyValidator.IsValidOpenAI, v => { OpenAiStatus = v; OpenAiHasKey = string.Equals(v, "Saved", StringComparison.Ordinal); });
    [RelayCommand] private void DeleteOpenAiKey() => DeleteKey("openai", v => { OpenAiStatus = v; OpenAiHasKey = false; OpenAiKey = ""; });

    [RelayCommand] private void SaveAnthropicKey() => SaveKey("anthropic", AnthropicKey, ApiKeyValidator.IsValidAnthropic, v => { AnthropicStatus = v; AnthropicHasKey = string.Equals(v, "Saved", StringComparison.Ordinal); });
    [RelayCommand] private void DeleteAnthropicKey() => DeleteKey("anthropic", v => { AnthropicStatus = v; AnthropicHasKey = false; AnthropicKey = ""; });

    [RelayCommand] private void SaveGeminiKey() => SaveKey("gemini", GeminiKey, ApiKeyValidator.IsValidGemini, v => { GeminiStatus = v; GeminiHasKey = string.Equals(v, "Saved", StringComparison.Ordinal); });
    [RelayCommand] private void DeleteGeminiKey() => DeleteKey("gemini", v => { GeminiStatus = v; GeminiHasKey = false; GeminiKey = ""; });

    [RelayCommand] private void SaveDeepgramKey() => SaveKey("deepgram", DeepgramKey, ApiKeyValidator.IsValidDeepgram, v => { DeepgramStatus = v; DeepgramHasKey = string.Equals(v, "Saved", StringComparison.Ordinal); });
    [RelayCommand] private void DeleteDeepgramKey() => DeleteKey("deepgram", v => { DeepgramStatus = v; DeepgramHasKey = false; DeepgramKey = ""; });

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void RefreshKeyStatus()
    {
        OpenAiHasKey = _storage.RetrieveKey("openai") is not null;
        OpenAiStatus = OpenAiHasKey ? "Saved" : "Not set";

        AnthropicHasKey = _storage.RetrieveKey("anthropic") is not null;
        AnthropicStatus = AnthropicHasKey ? "Saved" : "Not set";

        GeminiHasKey = _storage.RetrieveKey("gemini") is not null;
        GeminiStatus = GeminiHasKey ? "Saved" : "Not set";

        DeepgramHasKey = _storage.RetrieveKey("deepgram") is not null;
        DeepgramStatus = DeepgramHasKey ? "Saved" : "Not set";
    }

    private void SaveKey(string provider, string key, Func<string?, bool> validator, Action<string> setStatus)
    {
        if (!validator(key))
        {
            setStatus("Invalid format");
            return;
        }

        try
        {
            _storage.StoreKey(provider, key);
            setStatus("Saved");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save {Provider} key", provider);
            setStatus("Save failed");
        }
    }

    private void DeleteKey(string provider, Action<string> setStatus)
    {
        try
        {
            _storage.DeleteKey(provider);
            setStatus("Not set");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete {Provider} key", provider);
            setStatus("Delete failed");
        }
    }
}
