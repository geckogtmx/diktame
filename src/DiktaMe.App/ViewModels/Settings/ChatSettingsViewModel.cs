
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using Serilog;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// ViewModel for the Chat settings page.
/// Configures Chat overlay UI options and system prompts.
/// </summary>
public sealed partial class ChatSettingsViewModel : ObservableObject
{
    private readonly SettingsManager _settings;
    private readonly PipelineConfigManager _pipelineManager;

    // ── Chat UI settings ───────────────────────────────────────────────────

    [ObservableProperty]
    private int _fontSize = 14;

    [ObservableProperty]
    private bool _forgetOnClose;

    [ObservableProperty]
    private int _maxHistoryMessages = 50;

    [ObservableProperty]
    private double _windowOpacity = 0.95;

    [ObservableProperty]
    private bool _showTimestamps = true;

    [ObservableProperty]
    private bool _enableMarkdown = true;

    [ObservableProperty]
    private int _selectedThemeIndex;

    public string[] ThemeOptions { get; } = ["System", "Light", "Dark"];

    // ── Pipeline profiles (system prompts) ─────────────────────────────────

    [ObservableProperty]
    private string _cloudSystemPrompt = "";

    [ObservableProperty]
    private string _localSystemPrompt = "";

    public ChatSettingsViewModel(SettingsManager settings, PipelineConfigManager pipelineManager)
    {
        _settings = settings;
        _pipelineManager = pipelineManager;

        LoadSettings();
    }

    private void LoadSettings()
    {
        var chatSettings = _settings.Current.Chat;
        FontSize = chatSettings.FontSize;
        ForgetOnClose = chatSettings.ForgetOnClose;
        MaxHistoryMessages = chatSettings.MaxHistoryMessages;
        WindowOpacity = chatSettings.WindowOpacity;
        ShowTimestamps = chatSettings.ShowTimestamps;
        EnableMarkdown = chatSettings.EnableMarkdown;
        SelectedThemeIndex = Array.IndexOf(ThemeOptions, chatSettings.Theme);
        if (SelectedThemeIndex < 0) { SelectedThemeIndex = 0; } // Default to "System"

        // Load pipeline prompts
        var pipeline = _pipelineManager.GetPipelineByType("chat");
        if (pipeline is not null)
        {
            CloudSystemPrompt = pipeline.CloudProfile.SystemPrompt ?? "";
            LocalSystemPrompt = pipeline.LocalProfile.SystemPrompt ?? "";
        }
    }

    // ── Save settings ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            // Update Chat settings
            var newChatSettings = new ChatSettings
            {
                FontSize = FontSize,
                ForgetOnClose = ForgetOnClose,
                MaxHistoryMessages = MaxHistoryMessages,
                WindowOpacity = WindowOpacity,
                ShowTimestamps = ShowTimestamps,
                EnableMarkdown = EnableMarkdown,
                Theme = SelectedThemeIndex >= 0 && SelectedThemeIndex < ThemeOptions.Length
                    ? ThemeOptions[SelectedThemeIndex]
                    : "System",
            };

            var newSettings = _settings.Current with { Chat = newChatSettings };
            await _settings.UpdateAsync(newSettings).ConfigureAwait(false);

            // Update pipeline prompts
            var cloudProfile = new UtilityProfile
            {
                SystemPrompt = string.IsNullOrWhiteSpace(CloudSystemPrompt) ? null : CloudSystemPrompt,
                ModelName = null, // Chat uses provider default model
            };

            var localProfile = new UtilityProfile
            {
                SystemPrompt = string.IsNullOrWhiteSpace(LocalSystemPrompt) ? null : LocalSystemPrompt,
                ModelName = null,
            };

            await _pipelineManager.UpdatePipelineAsync("chat", cloudProfile, localProfile).ConfigureAwait(false);

            Log.Information("ChatSettingsVM: Saved settings");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChatSettingsVM: Failed to save settings");
        }
    }

    // ── Reset to defaults ──────────────────────────────────────────────────

    [RelayCommand]
    private void Reset()
    {
        var defaults = new ChatSettings();
        FontSize = defaults.FontSize;
        ForgetOnClose = defaults.ForgetOnClose;
        MaxHistoryMessages = defaults.MaxHistoryMessages;
        WindowOpacity = defaults.WindowOpacity;
        ShowTimestamps = defaults.ShowTimestamps;
        EnableMarkdown = defaults.EnableMarkdown;
        SelectedThemeIndex = Array.IndexOf(ThemeOptions, defaults.Theme);

        CloudSystemPrompt = PromptDefaults.Chat;
        LocalSystemPrompt = PromptDefaults.Chat;
    }
}
