
using InputSimulatorStandard;
using InputSimulatorStandard.Native;
using Serilog;

namespace DiktaMe.Core.Input;
/// <summary>
/// Injects text into the active foreground application using clipboard paste.
/// Preserves the user's existing clipboard content before and after injection.
/// Port of python/core/injector.py Injector class from V1.
/// </summary>
public sealed class TextInjector
{
    private readonly IInputSimulator _sim;

    /// <summary>
    /// The last text passed to <see cref="InjectText"/>.
    /// Volatile — cleared on application restart (same as V1).
    /// Used by the Oops hotkey to re-inject the previous result.
    /// </summary>
    public string? LastInjectedText { get; private set; }

    /// <summary>
    /// Initialises a new TextInjector with the default InputSimulator.
    /// </summary>
    public TextInjector() : this(new InputSimulator()) { }

    /// <summary>
    /// Initialises a new TextInjector with a custom <see cref="IInputSimulator"/>
    /// (used for testing / dependency injection).
    /// </summary>
    public TextInjector(IInputSimulator simulator)
    {
        _sim = simulator;
    }

    /// <summary>
    /// Re-injects the last text produced by <see cref="InjectText"/>.
    /// No-op when <see cref="LastInjectedText"/> is null or empty.
    /// </summary>
    /// <param name="trailingSpace">Passed through to <see cref="InjectText"/>.</param>
    /// <param name="additionalKey">Passed through to <see cref="InjectText"/>.</param>
    public void ReInjectLast(bool trailingSpace = true, string? additionalKey = null)
    {
        if (string.IsNullOrEmpty(LastInjectedText))
        {
            Log.Information("TextInjector: ReInjectLast — nothing stored");
            return;
        }

        Log.Information("TextInjector: re-injecting last text ({Chars} chars)", LastInjectedText.Length);

        // Wait for hotkey modifier keys to be released before pasting.
        // Oops (Ctrl+Alt+V) fires immediately — if we send Ctrl+V while Alt is
        // still held, the target app receives Ctrl+Alt+V instead of Ctrl+V.
        WaitForModifierRelease();

        InjectText(LastInjectedText, trailingSpace, additionalKey);
    }

    /// <summary>
    /// Polls until Ctrl, Alt, and Shift are all released, or a timeout is reached.
    /// Prevents hotkey modifier bleed into subsequent SendInput keystrokes.
    /// </summary>
    private static void WaitForModifierRelease(int timeoutMs = 1000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            bool ctrl = (NativeMethods.GetAsyncKeyState(0x11) & 0x8000) != 0;  // VK_CONTROL
            bool alt = (NativeMethods.GetAsyncKeyState(0x12) & 0x8000) != 0;   // VK_MENU
            bool shift = (NativeMethods.GetAsyncKeyState(0x10) & 0x8000) != 0; // VK_SHIFT

            if (!ctrl && !alt && !shift)
            {
                return;
            }

            Thread.Sleep(10);
        }

        Log.Warning("TextInjector: modifier keys still held after {TimeoutMs}ms — proceeding anyway", timeoutMs);
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(int vKey);
    }

    /// <summary>
    /// Injects <paramref name="text"/> into the active window via clipboard paste (Ctrl+V).
    /// Optionally appends a trailing space and presses an additional key after pasting.
    /// </summary>
    /// <param name="text">The text to inject.</param>
    /// <param name="trailingSpace">
    /// When <c>true</c> (default), a space is appended for natural word spacing between
    /// successive dictations.
    /// </param>
    /// <param name="additionalKey">
    /// Optional key to press after pasting: <c>"enter"</c>, <c>"return"</c>, or <c>"tab"</c>.
    /// Ignored if null or unrecognised.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="text"/> is null.
    /// </exception>
    public void InjectText(string text, bool trailingSpace = true, string? additionalKey = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        Log.Information("TextInjector: injecting {CharCount} chars (trailingSpace={TrailingSpace}, key={AdditionalKey})",
            text.Length, trailingSpace, additionalKey ?? "none");

        LastInjectedText = text;   // D.4: store for Oops re-inject

        // 1. Save original clipboard
        string original = ClipboardManager.GetText();

        // 2. Build text to paste
        string textToPaste = trailingSpace ? text + " " : text;

        // 3. Write to clipboard
        ClipboardManager.SetText(textToPaste);

        // Small delay to ensure clipboard is ready before Ctrl+V
        Thread.Sleep(50);

        // 4. Simulate Ctrl+V
        _sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_V);

        // 5. Wait for the paste to land before restoring clipboard
        Thread.Sleep(30);

        // 6. Restore original clipboard
        ClipboardManager.SetText(original);

        Log.Information("TextInjector: paste complete");

        // 7. Press optional additional key
        if (!string.IsNullOrWhiteSpace(additionalKey))
        {
            PressKey(additionalKey);
        }
    }

    /// <summary>
    /// Captures the currently selected text in the active window by sending Ctrl+C
    /// and reading the clipboard. Restores the original clipboard content on return.
    /// </summary>
    /// <param name="timeoutMs">
    /// Maximum time to wait for the clipboard to change after Ctrl+C (default: 1500 ms).
    /// </param>
    /// <returns>
    /// The captured text, or <c>null</c> if nothing was selected or the operation timed out.
    /// </returns>
    public string? CaptureSelection(int timeoutMs = 1500)
    {
        Log.Information("TextInjector: capturing selection (timeout={TimeoutMs}ms)", timeoutMs);

        // Wait for focus to return to the previously active window.
        // The global hotkey may have briefly stolen focus.
        Thread.Sleep(1000);

        // 1. Save current clipboard
        string original = ClipboardManager.GetText();

        // 2. Send Ctrl+C
        _sim.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);

        // Wait a moment for the clipboard to update
        Thread.Sleep(100);

        // 3. Poll for clipboard change
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            string current = ClipboardManager.GetText();

            if (!string.Equals(current, original, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(current))
            {
                Log.Information("TextInjector: captured {CharCount} chars", current.Length);

                // Restore original clipboard before returning
                ClipboardManager.SetText(original);
                return current;
            }

            Thread.Sleep(10);
        }

        Log.Warning("TextInjector: CaptureSelection timed out after {TimeoutMs}ms", timeoutMs);
        // Clipboard never changed — still in original state
        return null;
    }

    /// <summary>
    /// Presses a single named key: <c>"enter"</c>/<c>"return"</c> or <c>"tab"</c>.
    /// </summary>
    /// <param name="keyName">Case-insensitive key name.</param>
    public void PressKey(string keyName)
    {
        VirtualKeyCode? code = keyName.ToLowerInvariant() switch
        {
            "enter" or "return" => VirtualKeyCode.RETURN,
            "tab" => VirtualKeyCode.TAB,
            _ => null,
        };

        if (code is null)
        {
            Log.Warning("TextInjector: unknown key name '{KeyName}' — ignoring", keyName);
            return;
        }

        Log.Information("TextInjector: pressing key '{KeyName}'", keyName);
        _sim.Keyboard.KeyPress(code.Value);
    }
}
