using System.Text.Json;

namespace DiktaMe.Core.Config;

/// <summary>
/// Parsed IPC command from external tools (Stream Deck, automation scripts).
/// Commands arrive as newline-delimited JSON over a named pipe.
/// </summary>
public sealed record ApiCommand
{
    /// <summary>One of: "trigger", "toggle", "query".</summary>
    public required string Action { get; init; }

    /// <summary>For trigger: "dictate", "refine_auto", "refine_voice", "ask", "translate", "note", "oops", "read_selection".</summary>
    public string? Pipeline { get; init; }

    /// <summary>For trigger: optional dictation mode ID (GUID) to override the active mode.</summary>
    public string? ModeId { get; init; }

    /// <summary>For toggle: "RawModeOverride", "StreamingEnabled", "AudioDucking", "Engine".</summary>
    public string? Setting { get; init; }

    /// <summary>For query: "modes", "settings".</summary>
    public string? Target { get; init; }
}

/// <summary>
/// Parses newline-delimited JSON strings into <see cref="ApiCommand"/> records.
/// Returns null on malformed input rather than throwing.
/// </summary>
public static class ApiCommandParser
{
    public static ApiCommand? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionElement)
                || actionElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            string action = actionElement.GetString()!;
            if (string.IsNullOrEmpty(action))
            {
                return null;
            }

            return new ApiCommand
            {
                Action = action,
                Pipeline = GetOptionalString(root, "pipeline"),
                ModeId = GetOptionalString(root, "modeId"),
                Setting = GetOptionalString(root, "setting"),
                Target = GetOptionalString(root, "target"),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var element)
            && element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        return null;
    }
}
