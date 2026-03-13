
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// Host ViewModel for the Hardware &amp; Hotkeys settings page.
/// Aggregates AudioSettingsViewModel (microphone, ducking, sounds) and HotkeysSettingsViewModel (keyboard shortcuts).
/// </summary>
public sealed partial class HardwareSettingsViewModel : ObservableObject
{
    public AudioSettingsViewModel Audio { get; }
    public HotkeysSettingsViewModel Hotkeys { get; }

    public ObservableCollection<ModeListItem> SubItems { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isMicrophoneSelected;

    [ObservableProperty]
    private bool _isSoundFeedbackSelected;

    [ObservableProperty]
    private bool _isHotkeysSelected;

    public HardwareSettingsViewModel(
        AudioSettingsViewModel audio,
        HotkeysSettingsViewModel hotkeys)
    {
        Audio = audio;
        Hotkeys = hotkeys;

        LoadSubItems();

        if (SubItems.Count > 0)
        {
            SelectedIndex = 0;
        }
    }

    private void LoadSubItems()
    {
        SubItems.Clear();
        SubItems.Add(new ModeListItem
        {
            Id = "microphone",
            Title = "Microphone & Recording",
            IsDictationMode = false,
            IsSeparator = false,
        });
        SubItems.Add(new ModeListItem
        {
            Id = "soundfeedback",
            Title = "Sound Feedback",
            IsDictationMode = false,
            IsSeparator = false,
        });
        SubItems.Add(new ModeListItem
        {
            Id = "hotkeys",
            Title = "Keyboard Shortcuts",
            IsDictationMode = false,
            IsSeparator = false,
        });
    }

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < SubItems.Count;

        if (!HasSelection)
        {
            IsMicrophoneSelected = false;
            IsSoundFeedbackSelected = false;
            IsHotkeysSelected = false;
            return;
        }

        string id = SubItems[value].Id;
        IsMicrophoneSelected = id == "microphone";
        IsSoundFeedbackSelected = id == "soundfeedback";
        IsHotkeysSelected = id == "hotkeys";
    }
}
