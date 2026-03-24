using System.Globalization;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using DiktaMe.StreamDeck.Models;
using DiktaMe.StreamDeck.Services;
using Newtonsoft.Json.Linq;

namespace DiktaMe.StreamDeck.Actions;

[PluginActionId("me.dikta.streamdeck.toggle")]
internal sealed class SettingsToggleAction : KeypadBase
{
    private readonly ToggleActionSettings _settings;
    private bool _currentValue;

    public SettingsToggleAction(ISDConnection connection, InitialPayload payload)
        : base(connection, payload)
    {
        _settings = payload.Settings is { Count: > 0 }
            ? payload.Settings.ToObject<ToggleActionSettings>() ?? new ToggleActionSettings()
            : new ToggleActionSettings();

        ApiPipeClient.Instance.EnsureStarted();

        ApiPipeClient.Instance.SettingsReceived += OnSettingsReceived;
        ApiPipeClient.Instance.ConnectionChanged += OnConnectionChanged;

        // Apply cached settings if available
        if (ApiPipeClient.Instance.CurrentSettingsJson is not null)
        {
            ApplySettingsValue(ApiPipeClient.Instance.CurrentSettingsJson);
        }

        _ = UpdateIconAsync();
    }

    public override void Dispose()
    {
        ApiPipeClient.Instance.SettingsReceived -= OnSettingsReceived;
        ApiPipeClient.Instance.ConnectionChanged -= OnConnectionChanged;
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        if (!ApiPipeClient.Instance.IsConnected)
        {
            await Connection.ShowAlert().ConfigureAwait(false);
            return;
        }

        string json = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"action\":\"toggle\",\"setting\":\"{0}\"}}",
            _settings.SettingName);

        await ApiPipeClient.Instance.SendCommandAsync(json).ConfigureAwait(false);
        // Don't update icon here — wait for the settings event from the app.
    }

    public override void KeyReleased(KeyPayload payload) { }
    public override void OnTick() { }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        Tools.AutoPopulateSettings(_settings, payload.Settings);
        _ = Connection.SetSettingsAsync(JObject.FromObject(_settings));
        _ = UpdateIconAsync();
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

    // ── Event handlers ──────────────────────────────────────────────

    private void OnSettingsReceived(JObject json)
    {
        ApplySettingsValue(json);
        _ = UpdateIconAsync();
    }

    private void OnConnectionChanged(bool connected)
    {
        _ = UpdateIconAsync();
    }

    private void ApplySettingsValue(JObject json)
    {
        _currentValue = _settings.SettingName switch
        {
            "RawModeOverride" => json.Value<bool>("RawModeOverride"),
            "StreamingEnabled" => json.Value<bool>("StreamingEnabled"),
            "AudioDucking" => json.Value<bool>("AudioDucking"),
            "Engine" => string.Equals(
                json.Value<string>("ActiveProfile"),
                "Local",
                StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    // ── Icon management ─────────────────────────────────────────────

    private async Task UpdateIconAsync()
    {
        bool connected = ApiPipeClient.Instance.IsConnected;

        string imagePath = !connected
            ? @"Images\toggle-offline"
            : _currentValue
                ? @"Images\toggle-on"
                : @"Images\toggle-off";

        await Connection.SetImageAsync(
            Tools.FileToBase64(imagePath + ".png", true)).ConfigureAwait(false);

        await Connection.SetTitleAsync(GetSettingLabel()).ConfigureAwait(false);
    }

    private string GetSettingLabel()
    {
#pragma warning disable MA0110
        return _settings.SettingName switch
        {
            "RawModeOverride" => _currentValue ? "RAW ON" : "RAW",
            "StreamingEnabled" => _currentValue ? "STREAM" : "BATCH",
            "AudioDucking" => _currentValue ? "DUCK ON" : "DUCK",
            "Engine" => _currentValue ? "LOCAL" : "CLOUD",
            _ => _settings.SettingName,
        };
#pragma warning restore MA0110
    }
}
