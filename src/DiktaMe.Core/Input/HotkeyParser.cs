namespace DiktaMe.Core.Input;

/// <summary>
/// Parses hotkey strings like <c>"Ctrl+Alt+D"</c> into Win32 modifier flags and VK code.
/// </summary>
internal static class HotkeyParser
{
    // Win32 modifier flags (fsModifiers parameter of RegisterHotKey)
    private const uint ModAlt     = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift   = 0x0004;
    private const uint ModWin     = 0x0008;

    // Modifier token normalisation map
    private static readonly Dictionary<string, uint> ModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"]    = ModControl,
        ["control"] = ModControl,
        ["alt"]     = ModAlt,
        ["shift"]   = ModShift,
        ["win"]     = ModWin,
        ["windows"] = ModWin,
    };

    // Single-character VK map for letter and digit keys
    // Win32 VK codes for A-Z are 0x41-0x5A; for 0-9 are 0x30-0x39
    // For everything else we use the VirtualKey enum names via Enum.Parse

    // Named key map for special keys
    private static readonly Dictionary<string, uint> NamedKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["f1"]     = 0x70, ["f2"]  = 0x71, ["f3"]  = 0x72, ["f4"]  = 0x73,
        ["f5"]     = 0x74, ["f6"]  = 0x75, ["f7"]  = 0x76, ["f8"]  = 0x77,
        ["f9"]     = 0x78, ["f10"] = 0x79, ["f11"] = 0x7A, ["f12"] = 0x7B,
        ["space"]  = 0x20,
        ["enter"]  = 0x0D,
        ["return"] = 0x0D,
        ["tab"]    = 0x09,
        ["esc"]    = 0x1B, ["escape"] = 0x1B,
        ["home"]   = 0x24, ["end"]    = 0x23,
        ["pageup"] = 0x21, ["pagedown"] = 0x22,
        ["left"]   = 0x25, ["right"]  = 0x27,
        ["up"]     = 0x26, ["down"]   = 0x28,
        ["insert"] = 0x2D, ["delete"] = 0x2E,
        ["backspace"] = 0x08,
        ["numpad0"] = 0x60, ["numpad1"] = 0x61, ["numpad2"] = 0x62,
        ["numpad3"] = 0x63, ["numpad4"] = 0x64, ["numpad5"] = 0x65,
        ["numpad6"] = 0x66, ["numpad7"] = 0x67, ["numpad8"] = 0x68,
        ["numpad9"] = 0x69,
    };

    /// <summary>
    /// Attempts to parse a hotkey string into Win32 modifier flags and a virtual key code.
    /// </summary>
    /// <param name="hotkeyString">
    /// E.g. <c>"Ctrl+Alt+D"</c>, <c>"Control+Shift+F1"</c>, <c>"Win+Space"</c>.
    /// </param>
    /// <param name="modifiers">Resulting modifier flags for RegisterHotKey.</param>
    /// <param name="vk">Resulting virtual key code.</param>
    /// <returns><c>true</c> if parsing succeeded.</returns>
    internal static bool TryParse(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        string[] parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        uint keyCode = 0;

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];

            if (ModifierMap.TryGetValue(part, out uint mod))
            {
                modifiers |= mod;
                continue;
            }

            // Last non-modifier token is the key itself
            if (!TryParseKey(part, out keyCode))
                return false;
        }

        if (keyCode == 0)
            return false;

        vk = keyCode;
        return true;
    }

    private static bool TryParseKey(string token, out uint vk)
    {
        vk = 0;

        // Single letter A-Z
        if (token.Length == 1)
        {
            char c = char.ToUpperInvariant(token[0]);
            if (c >= 'A' && c <= 'Z')
            {
                vk = (uint)c; // VK_A == 0x41 == 'A'
                return true;
            }
            if (c >= '0' && c <= '9')
            {
                vk = (uint)c; // VK_0 == 0x30 == '0'
                return true;
            }
        }

        // Named keys (F1-F12, Enter, Space, etc.)
        if (NamedKeyMap.TryGetValue(token, out vk))
            return true;

        return false;
    }
}
