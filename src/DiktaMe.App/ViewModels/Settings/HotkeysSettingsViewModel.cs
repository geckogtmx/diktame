
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;
public sealed partial class HotkeysSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private bool _isLoading;

    [ObservableProperty]
    private string _dictate = "Ctrl+Alt+D";

    [ObservableProperty]
    private string _refine = "Ctrl+Alt+R";

    [ObservableProperty]
    private string _ask = "Ctrl+Alt+A";

    [ObservableProperty]
    private string _translate = "Ctrl+Alt+T";

    [ObservableProperty]
    private string _oops = "Ctrl+Alt+V";

    [ObservableProperty]
    private string _note = "Ctrl+Alt+N";

    [ObservableProperty]
    private string _chat = "Ctrl+Alt+C";

    [ObservableProperty]
    private string _readSelection = "Ctrl+Alt+Q";

    [ObservableProperty]
    private string _vision = "Ctrl+Alt+S";

    [ObservableProperty]
    private string? _recordingTarget;

    public HotkeysSettingsViewModel(SettingsManager settings)
    {
        _settings = settings;
        LoadFromSettings();
    }

    private void LoadFromSettings()
    {
        _isLoading = true;
        var h = _settings.Current.Hotkeys;

        Dictate = h.Dictate;
        Refine = h.Refine;
        Ask = h.Ask;
        Translate = h.Translate;
        Oops = h.Oops;
        Note = h.Note;
        Chat = h.Chat;
        ReadSelection = h.ReadSelection;
        Vision = h.Vision;

        _isLoading = false;
    }

    partial void OnDictateChanged(string value) => Save();
    partial void OnRefineChanged(string value) => Save();
    partial void OnAskChanged(string value) => Save();
    partial void OnTranslateChanged(string value) => Save();
    partial void OnOopsChanged(string value) => Save();
    partial void OnNoteChanged(string value) => Save();
    partial void OnChatChanged(string value) => Save();
    partial void OnReadSelectionChanged(string value) => Save();
    partial void OnVisionChanged(string value) => Save();

    private void Save()
    {
        if (_isLoading)
        {
            return;
        }

        var updated = _settings.Current with
        {
            Hotkeys = new HotkeySettings
            {
                Dictate = Dictate,
                Refine = Refine,
                Ask = Ask,
                Translate = Translate,
                Oops = Oops,
                Note = Note,
                Chat = Chat,
                ReadSelection = ReadSelection,
                Vision = Vision,
            }
        };
        _ = _settings.UpdateAsync(updated).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                Log.Error(t.Exception, "Failed to save hotkey settings");
            }
        }, TaskScheduler.Default);
    }

    [RelayCommand]
    private void ResetDictate() => Dictate = "Ctrl+Alt+D";

    [RelayCommand]
    private void ResetRefine() => Refine = "Ctrl+Alt+R";

    [RelayCommand]
    private void ResetAsk() => Ask = "Ctrl+Alt+A";

    [RelayCommand]
    private void ResetTranslate() => Translate = "Ctrl+Alt+T";

    [RelayCommand]
    private void ResetOops() => Oops = "Ctrl+Alt+V";

    [RelayCommand]
    private void ResetNote() => Note = "Ctrl+Alt+N";

    [RelayCommand]
    private void ResetChat() => Chat = "Ctrl+Alt+C";

    [RelayCommand]
    private void ResetReadSelection() => ReadSelection = "Ctrl+Alt+Q";

    [RelayCommand]
    private void ResetVision() => Vision = "Ctrl+Alt+S";

    public void SetRecordingTarget(string? target)
    {
        RecordingTarget = target;
    }

    public void SetHotkeyValue(string target, string value)
    {
        _isLoading = true;
        switch (target)
        {
            case "Dictate":
                Dictate = value;
                break;
            case "Refine":
                Refine = value;
                break;
            case "Ask":
                Ask = value;
                break;
            case "Translate":
                Translate = value;
                break;
            case "Oops":
                Oops = value;
                break;
            case "Note":
                Note = value;
                break;
            case "Chat":
                Chat = value;
                break;
            case "ReadSelection":
                ReadSelection = value;
                break;
            case "Vision":
                Vision = value;
                break;
        }
        _isLoading = false;
        Save();
    }
}
