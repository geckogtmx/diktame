using Newtonsoft.Json;

namespace DiktaMe.StreamDeck.Models;

internal sealed class ToggleActionSettings
{
    /// <summary>
    /// Setting to toggle: RawModeOverride, StreamingEnabled, AudioDucking, Engine.
    /// </summary>
    [JsonProperty("settingName")]
    public string SettingName { get; set; } = "RawModeOverride";
}
