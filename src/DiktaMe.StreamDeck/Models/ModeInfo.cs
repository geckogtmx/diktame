using Newtonsoft.Json;

namespace DiktaMe.StreamDeck.Models;

internal sealed class ModeInfo
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("title")]
    public string Title { get; set; } = "";
}
