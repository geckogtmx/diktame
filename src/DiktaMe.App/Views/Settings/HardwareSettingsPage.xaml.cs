
using DiktaMe.App.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace DiktaMe.App.Views.Settings;

/// <summary>
/// Audio &amp; Hotkeys settings page — hosts Microphone, Sound Feedback, and Keyboard Shortcuts sub-items.
/// The hotkey recording logic is copied from HotkeysSettingsPage (code-behind key capture).
/// </summary>
public sealed partial class HardwareSettingsPage : Page
{
    public HardwareSettingsViewModel ViewModel { get; }
    private TextBox? _recordingTextBox;
    private string? _recordingTarget;
    private SolidColorBrush? _normalBorderBrush;
    private readonly SolidColorBrush _recordingBorderBrush;

    public HardwareSettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<HardwareSettingsViewModel>();
        this.InitializeComponent();

        _recordingBorderBrush = new SolidColorBrush(Colors.Orange);
    }

    // ── Hotkey Recording Click Handlers ─────────────────────────────────────

    private void RecordDictate_Click(object sender, RoutedEventArgs e) => StartRecording(DictateBox, "Dictate");
    private void RecordRefine_Click(object sender, RoutedEventArgs e) => StartRecording(RefineBox, "Refine");
    private void RecordAsk_Click(object sender, RoutedEventArgs e) => StartRecording(AskBox, "Ask");
    private void RecordTranslate_Click(object sender, RoutedEventArgs e) => StartRecording(TranslateBox, "Translate");
    private void RecordOops_Click(object sender, RoutedEventArgs e) => StartRecording(OopsBox, "Oops");
    private void RecordNote_Click(object sender, RoutedEventArgs e) => StartRecording(NoteBox, "Note");
    private void RecordChat_Click(object sender, RoutedEventArgs e) => StartRecording(ChatBox, "Chat");
    private void RecordReadSelection_Click(object sender, RoutedEventArgs e) => StartRecording(ReadSelectionBox, "ReadSelection");

    private void StartRecording(TextBox textBox, string target)
    {
        StopRecording();

        _recordingTextBox = textBox;
        _recordingTarget = target;

        if (_normalBorderBrush == null)
        {
            _normalBorderBrush = textBox.BorderBrush as SolidColorBrush;
        }

        textBox.BorderBrush = _recordingBorderBrush;
        textBox.BorderThickness = new Thickness(3);
        textBox.PlaceholderText = "Press key combination...";

        textBox.Focus(FocusState.Programmatic);
        textBox.KeyDown += TextBox_KeyDown;
        textBox.LostFocus += TextBox_LostFocus;

        ViewModel.Hotkeys.SetRecordingTarget(target);
    }

    private void StopRecording()
    {
        if (_recordingTextBox == null)
        {
            return;
        }

        if (_normalBorderBrush != null)
        {
            _recordingTextBox.BorderBrush = _normalBorderBrush;
        }
        _recordingTextBox.BorderThickness = new Thickness(2);
        _recordingTextBox.PlaceholderText = "Click Record to set hotkey";
        _recordingTextBox.KeyDown -= TextBox_KeyDown;
        _recordingTextBox.LostFocus -= TextBox_LostFocus;

        _recordingTextBox = null;
        _recordingTarget = null;
        ViewModel.Hotkeys.SetRecordingTarget(null);
    }

    private void TextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_recordingTarget == null || _recordingTextBox == null)
        {
            return;
        }

        e.Handled = true;

        bool ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        bool alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        bool shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        bool win = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.LeftWindows).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) ||
                   Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.RightWindows).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        VirtualKey key = (VirtualKey)e.OriginalKey;
        if (key == VirtualKey.Control || key == VirtualKey.Menu || key == VirtualKey.Shift ||
            key == VirtualKey.LeftWindows || key == VirtualKey.RightWindows ||
            key == VirtualKey.LeftControl || key == VirtualKey.RightControl ||
            key == VirtualKey.LeftMenu || key == VirtualKey.RightMenu ||
            key == VirtualKey.LeftShift || key == VirtualKey.RightShift)
        {
            return;
        }

        if (!ctrl && !alt && !shift && !win)
        {
            return;
        }

        var parts = new System.Collections.Generic.List<string>();
        if (ctrl) { parts.Add("Ctrl"); }
        if (alt) { parts.Add("Alt"); }
        if (shift) { parts.Add("Shift"); }
        if (win) { parts.Add("Win"); }

        string keyName = MapKeyToString(key);
        parts.Add(keyName);

        string hotkeyString = string.Join("+", parts);

        ViewModel.Hotkeys.SetHotkeyValue(_recordingTarget, hotkeyString);

        StopRecording();
    }

    private void TextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        StopRecording();
    }

    private static string MapKeyToString(VirtualKey key)
    {
        if (key >= VirtualKey.A && key <= VirtualKey.Z)
        {
            return key.ToString();
        }

        if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            return ((char)('0' + (key - VirtualKey.Number0))).ToString();
        }

        if (key >= VirtualKey.F1 && key <= VirtualKey.F24)
        {
            int fNum = key - VirtualKey.F1 + 1;
            return $"F{fNum}";
        }

        if (key >= VirtualKey.NumberPad0 && key <= VirtualKey.NumberPad9)
        {
            int num = key - VirtualKey.NumberPad0;
            return $"Numpad{num}";
        }

        return key switch
        {
            VirtualKey.Space => "Space",
            VirtualKey.Enter => "Enter",
            VirtualKey.Tab => "Tab",
            VirtualKey.Escape => "Esc",
            VirtualKey.Home => "Home",
            VirtualKey.End => "End",
            VirtualKey.PageUp => "PageUp",
            VirtualKey.PageDown => "PageDown",
            VirtualKey.Left => "Left",
            VirtualKey.Right => "Right",
            VirtualKey.Up => "Up",
            VirtualKey.Down => "Down",
            VirtualKey.Insert => "Insert",
            VirtualKey.Delete => "Delete",
            VirtualKey.Back => "Backspace",
            _ => key.ToString()
        };
    }
}
