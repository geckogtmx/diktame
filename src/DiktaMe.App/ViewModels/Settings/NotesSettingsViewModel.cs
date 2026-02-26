
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using Serilog;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// ViewModel for the Notes settings page.
/// Configures voice-to-note pipeline: file path, LLM processing, timestamp format.
/// </summary>
public sealed partial class NotesSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly PipelineConfigManager _pipelineManager;

    // ── Note file configuration ─────────────────────────────────────────────

    [ObservableProperty]
    private string _filePath = "";

    [ObservableProperty]
    private bool _useLlmProcessing = true;

    [ObservableProperty]
    private string _timestampFormat = "";

    // ── Pipeline profiles (system prompts) ─────────────────────────────────

    [ObservableProperty]
    private string _cloudSystemPrompt = "";

    [ObservableProperty]
    private string _localSystemPrompt = "";

    // ── Live preview ───────────────────────────────────────────────────────

    [ObservableProperty]
    private string _previewText = "";

    public NotesSettingsViewModel(SettingsManager settings, PipelineConfigManager pipelineManager)
    {
        _settings = settings;
        _pipelineManager = pipelineManager;

        LoadSettings();
        UpdatePreview();
    }

    private void LoadSettings()
    {
        var noteSettings = _settings.Current.Note;
        FilePath = noteSettings.FilePath;
        UseLlmProcessing = noteSettings.UseLlmProcessing;
        TimestampFormat = noteSettings.TimestampFormat;

        // Load pipeline prompts
        var pipeline = _pipelineManager.GetPipelineByType("note");
        if (pipeline is not null)
        {
            CloudSystemPrompt = pipeline.CloudProfile.SystemPrompt ?? "";
            LocalSystemPrompt = pipeline.LocalProfile.SystemPrompt ?? "";
        }
    }

    // ── Browse for file path ───────────────────────────────────────────────

    [RelayCommand]
    private async Task BrowseFilePathAsync()
    {
        var picker = new FileSavePicker();
        picker.SuggestedFileName = Path.GetFileName(FilePath);
        picker.FileTypeChoices.Add("Markdown files", new List<string> { ".md" });
        picker.FileTypeChoices.Add("Text files", new List<string> { ".txt" });
        picker.FileTypeChoices.Add("All files", new List<string> { ".*" });

        // Get the window handle
        var window = App.Current.MainWindow;
        var hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            FilePath = file.Path;
        }
    }

    // ── Save settings ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            // Update Note settings
            var newNoteSettings = new NoteSettings
            {
                FilePath = FilePath,
                UseLlmProcessing = UseLlmProcessing,
                TimestampFormat = TimestampFormat,
            };

            var newSettings = _settings.Current with { Note = newNoteSettings };
            await _settings.UpdateAsync(newSettings).ConfigureAwait(false);

            // Update pipeline prompts
            var cloudProfile = new UtilityProfile
            {
                SystemPrompt = string.IsNullOrWhiteSpace(CloudSystemPrompt) ? null : CloudSystemPrompt,
                ModelName = null, // Note uses provider default model
            };

            var localProfile = new UtilityProfile
            {
                SystemPrompt = string.IsNullOrWhiteSpace(LocalSystemPrompt) ? null : LocalSystemPrompt,
                ModelName = null,
            };

            await _pipelineManager.UpdatePipelineAsync("note", cloudProfile, localProfile).ConfigureAwait(false);

            Log.Information("NotesSettingsVM: Saved settings");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NotesSettingsVM: Failed to save settings");
        }
    }

    // ── Reset to defaults ──────────────────────────────────────────────────

    [RelayCommand]
    private void Reset()
    {
        var defaults = new NoteSettings();
        FilePath = defaults.FilePath;
        UseLlmProcessing = defaults.UseLlmProcessing;
        TimestampFormat = defaults.TimestampFormat;

        CloudSystemPrompt = PromptDefaults.Note;
        LocalSystemPrompt = PromptDefaults.Note;

        UpdatePreview();
    }

    // ── Update live preview ────────────────────────────────────────────────

    partial void OnTimestampFormatChanged(string value)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        try
        {
            string timestamp = DateTime.Now.ToString(TimestampFormat, System.Globalization.CultureInfo.InvariantCulture);
            PreviewText = $"[{timestamp}]\nSample transcription...\n---";
        }
        catch (FormatException)
        {
            PreviewText = "[Invalid format]\nSample transcription...\n---";
        }
    }
}
