
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// Host ViewModel for the Hardware settings page.
/// Aggregates AudioSettingsViewModel (microphone, ducking, sounds).
/// </summary>
public sealed partial class HardwareSettingsViewModel : ObservableObject
{
    public AudioSettingsViewModel Audio { get; }

    public ObservableCollection<ModeListItem> SubItems { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isMicrophoneSelected;

    [ObservableProperty]
    private bool _isSoundFeedbackSelected;

    public HardwareSettingsViewModel(AudioSettingsViewModel audio)
    {
        Audio = audio;

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
    }

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < SubItems.Count;

        if (!HasSelection)
        {
            IsMicrophoneSelected = false;
            IsSoundFeedbackSelected = false;
            return;
        }

        string id = SubItems[value].Id;
        IsMicrophoneSelected = id == "microphone";
        IsSoundFeedbackSelected = id == "soundfeedback";
    }
}
