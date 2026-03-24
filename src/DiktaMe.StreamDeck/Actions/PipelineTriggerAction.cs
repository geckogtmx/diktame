using System.Globalization;
using BarRaider.SdTools;
using BarRaider.SdTools.Wrappers;
using DiktaMe.StreamDeck.Models;
using DiktaMe.StreamDeck.Services;
using Newtonsoft.Json.Linq;

namespace DiktaMe.StreamDeck.Actions;

[PluginActionId("me.dikta.streamdeck.trigger")]
internal sealed class PipelineTriggerAction : KeypadBase
{
    private readonly TriggerActionSettings _settings;
    private string _lastKnownState = "Idle";
    private bool _showingBusy;

    public PipelineTriggerAction(ISDConnection connection, InitialPayload payload)
        : base(connection, payload)
    {
        _settings = payload.Settings is { Count: > 0 }
            ? payload.Settings.ToObject<TriggerActionSettings>() ?? new TriggerActionSettings()
            : new TriggerActionSettings();

        ApiPipeClient.Instance.EnsureStarted();

        ApiPipeClient.Instance.StateChanged += OnStateChanged;
        ApiPipeClient.Instance.BusyReceived += OnBusy;
        ApiPipeClient.Instance.ConnectionChanged += OnConnectionChanged;
        ApiPipeClient.Instance.ModesReceived += OnModesReceived;
        Connection.OnPropertyInspectorDidAppear += OnPropertyInspectorAppeared;

        _lastKnownState = ApiPipeClient.Instance.IsConnected
            ? ApiPipeClient.Instance.CurrentPipelineState
            : "Offline";

        _ = UpdateIconAsync(_lastKnownState);
    }

    public override void Dispose()
    {
        ApiPipeClient.Instance.StateChanged -= OnStateChanged;
        ApiPipeClient.Instance.BusyReceived -= OnBusy;
        ApiPipeClient.Instance.ConnectionChanged -= OnConnectionChanged;
        ApiPipeClient.Instance.ModesReceived -= OnModesReceived;
        Connection.OnPropertyInspectorDidAppear -= OnPropertyInspectorAppeared;
    }

    public override async void KeyPressed(KeyPayload payload)
    {
        if (!ApiPipeClient.Instance.IsConnected)
        {
            await Connection.ShowAlert().ConfigureAwait(false);
            return;
        }

        string pipeline = _settings.PipelineType;
        string json;

        if (string.Equals(pipeline, "dictate", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(_settings.ModeId))
        {
            json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"action\":\"trigger\",\"pipeline\":\"{0}\",\"modeId\":\"{1}\"}}",
                pipeline,
                _settings.ModeId);
        }
        else
        {
            json = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"action\":\"trigger\",\"pipeline\":\"{0}\"}}",
                pipeline);
        }

        await ApiPipeClient.Instance.SendCommandAsync(json).ConfigureAwait(false);
    }

    public override void KeyReleased(KeyPayload payload) { }

    public override void OnTick()
    {
        if (_showingBusy)
        {
            _showingBusy = false;
            _ = UpdateIconAsync(_lastKnownState);
        }
    }

    public override void ReceivedSettings(ReceivedSettingsPayload payload)
    {
        Tools.AutoPopulateSettings(_settings, payload.Settings);
        _ = Connection.SetSettingsAsync(JObject.FromObject(_settings));
        _ = UpdateIconAsync(_lastKnownState);
    }

    public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

    // ── Event handlers ──────────────────────────────────────────────

    private void OnStateChanged(string state)
    {
        _lastKnownState = state;
        _ = UpdateIconAsync(state);
    }

    private void OnBusy()
    {
        _showingBusy = true;
        _ = Connection.ShowAlert();
    }

    private void OnConnectionChanged(bool connected)
    {
        string state = connected ? ApiPipeClient.Instance.CurrentPipelineState : "Offline";
        _lastKnownState = state;
        _ = UpdateIconAsync(state);
        _ = SendConnectionStatusAsync(connected);
    }

    private void OnModesReceived(JArray modes)
    {
        _ = SendModesToPiAsync(modes);
    }

    private void OnPropertyInspectorAppeared(object? sender, SDEventReceivedEventArgs<BarRaider.SdTools.Events.PropertyInspectorDidAppear> e)
    {
        // PI just opened — send current state so it doesn't stay on "Connecting..."
        _ = SendConnectionStatusAsync(ApiPipeClient.Instance.IsConnected);

        // Send cached modes list if available
        if (ApiPipeClient.Instance.CurrentModes is { Count: > 0 })
        {
            var modesArray = JArray.FromObject(ApiPipeClient.Instance.CurrentModes);
            _ = SendModesToPiAsync(modesArray);
        }
    }

    private async Task SendModesToPiAsync(JArray modes)
    {
        var piData = new JObject
        {
            ["event"] = "modesUpdate",
            ["modes"] = modes,
        };
        await Connection.SendToPropertyInspectorAsync(piData).ConfigureAwait(false);
    }

    private async Task SendConnectionStatusAsync(bool connected)
    {
        var piData = new JObject
        {
            ["event"] = "connectionStatus",
            ["status"] = connected ? "Connected" : "Offline",
        };
        await Connection.SendToPropertyInspectorAsync(piData).ConfigureAwait(false);
    }

    // ── Icon management ─────────────────────────────────────────────

    private async Task UpdateIconAsync(string state)
    {
        bool isActive = state is "Recording" or "Transcribing" or "Streaming"
                        or "Processing" or "Injecting" or "Speaking";
        bool isOffline = string.Equals(state, "Offline", StringComparison.Ordinal);

        string imagePath = isOffline
            ? @"Images\trigger-offline"
            : isActive
                ? @"Images\trigger-active"
                : @"Images\trigger-idle";

        await Connection.SetImageAsync(
            Tools.FileToBase64(imagePath + ".png", true)).ConfigureAwait(false);

        await Connection.SetTitleAsync(GetPipelineLabel()).ConfigureAwait(false);
    }

    private string GetPipelineLabel()
    {
#pragma warning disable MA0110 // Switch expression is cleaner without culture transform
        return _settings.PipelineType switch
        {
            "dictate" => "Dictate",
            "ask" => "Ask",
            "refine_auto" => "Refine",
            "refine_voice" => "Refine V",
            "translate" => "Translate",
            "note" => "Note",
            "oops" => "Oops",
            "read_selection" => "Read",
            _ => _settings.PipelineType,
        };
#pragma warning restore MA0110
    }
}
