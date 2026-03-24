using Newtonsoft.Json;

namespace DiktaMe.StreamDeck.Models;

internal sealed class TriggerActionSettings
{
    /// <summary>
    /// Pipeline type: dictate, ask, refine_auto, refine_voice, translate, note, oops, read_selection.
    /// </summary>
    [JsonProperty("pipelineType")]
    public string PipelineType { get; set; } = "dictate";

    /// <summary>
    /// Dictation mode ID (GUID). Empty string means "use app default".
    /// </summary>
    [JsonProperty("modeId")]
    public string ModeId { get; set; } = "";
}
