
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.App.Services;
using DiktaMe.Core.Config;
using DiktaMe.Core.LLM;
using Serilog;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DiktaMe.App.ViewModels.Settings;

/// <summary>
/// Unified ViewModel for the Modes settings page.
/// Manages Ask, Refine (Auto), Refine (Verbal), Translate, Notes, and Chat modes.
/// Uses PipelineConfigManager for pipeline settings and ModelListService for real-time model discovery.
/// </summary>
public sealed partial class ModesSettingsViewModel : ObservableObject
{
    private readonly PipelineConfigManager _pipelineManager;
    private readonly SettingsManager _settings;
    private readonly ModelListService _modelListService;
    private readonly LocalizationService _loc;

    // ── Left sidebar list ──────────────────────────────────────────────────

    /// <summary>6 fixed utility pipeline items.</summary>
    public ObservableCollection<ModeListItem> ModeItems { get; } = [];

    [ObservableProperty]
    private int _selectedIndex = -1;

    // ── Common fields (all modes) ──────────────────────────────────────────

    [ObservableProperty]
    private string _cloudSystemPrompt = "";

    [ObservableProperty]
    private string _localSystemPrompt = "";

    [ObservableProperty]
    private int _selectedCloudModelIndex;

    [ObservableProperty]
    private int _selectedLocalModelIndex;

    // ── Model lists ────────────────────────────────────────────────────────

    /// <summary>Display names for Cloud model ComboBox (no Ollama models).</summary>
    public ObservableCollection<string> CloudModelNames { get; } = [];

    /// <summary>Backing model IDs for Cloud models (parallel to CloudModelNames).</summary>
    private readonly List<string> _cloudModelIds = [];

    /// <summary>Display names for Local model ComboBox (Ollama only).</summary>
    public ObservableCollection<string> LocalModelNames { get; } = [];

    /// <summary>Backing model IDs for Local models (parallel to LocalModelNames).</summary>
    private readonly List<string> _localModelIds = [];

    [ObservableProperty]
    private bool _isLoadingModels;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private string _selectedModeTitle = "";

    // ── Ask mode fields ────────────────────────────────────────────────────

    [ObservableProperty]
    private int _selectedAskOutputIndex;

    public string[] AskOutputOptions => [
        _loc.GetString("Settings_Modes_AskOutput_ToastOnly"),
        _loc.GetString("Settings_Modes_AskOutput_ClipboardOnly"),
        _loc.GetString("Settings_Modes_AskOutput_InjectOnly"),
        _loc.GetString("Settings_Modes_AskOutput_ClipboardToast"),
    ];

    [ObservableProperty]
    private bool _isAskSelected;

    // ── Notes mode fields ──────────────────────────────────────────────────

    [ObservableProperty]
    private string _noteFilePath = "";

    [ObservableProperty]
    private bool _noteUseLlmProcessing = true;

    [ObservableProperty]
    private string _noteTimestampFormat = "";

    [ObservableProperty]
    private string _notePreviewText = "";

    [ObservableProperty]
    private bool _isNoteSelected;

    // ── Chat mode fields ───────────────────────────────────────────────────

    [ObservableProperty]
    private int _chatFontSize = 14;

    [ObservableProperty]
    private double _chatWindowOpacity = 0.95;

    [ObservableProperty]
    private int _chatSelectedThemeIndex;

    public string[] ChatThemeOptions => [
        _loc.GetString("Settings_Modes_ChatTheme_System"),
        _loc.GetString("Settings_Modes_ChatTheme_Light"),
        _loc.GetString("Settings_Modes_ChatTheme_Dark"),
    ];
    private static readonly string[] ChatThemeCodes = ["System", "Light", "Dark"];

    [ObservableProperty]
    private bool _chatForgetOnClose;

    [ObservableProperty]
    private int _chatMaxHistoryMessages = 50;

    [ObservableProperty]
    private bool _chatShowTimestamps = true;

    [ObservableProperty]
    private bool _chatEnableMarkdown = true;

    [ObservableProperty]
    private bool _chatAlwaysOnTop = true;

    [ObservableProperty]
    private string _chatDefaultModelId = "";

    [ObservableProperty]
    private string _chatDefaultSystemPrompt = "";

    [ObservableProperty]
    private bool _chatWebSearchEnabled;

    [ObservableProperty]
    private bool _isChatSelected;

    /// <summary>True if Reset button should be visible (Notes or Chat selected).</summary>
    public bool CanReset => IsNoteSelected || IsChatSelected;

    /// <summary>True if system prompt fields should be visible (all modes except Chat).</summary>
    public bool ShowPromptFields => HasSelection && !IsChatSelected;

    // ── Constructor ────────────────────────────────────────────────────────

    public ModesSettingsViewModel(
        PipelineConfigManager pipelineManager,
        SettingsManager settings,
        ModelListService modelListService,
        LocalizationService loc)
    {
        _pipelineManager = pipelineManager;
        _settings = settings;
        _modelListService = modelListService;
        _loc = loc;

        LoadModeList();
        _ = LoadModelsAsync();

        // Auto-select first item
        if (ModeItems.Count > 0)
        {
            SelectedIndex = 0;
        }
    }

    // ── List loading ───────────────────────────────────────────────────────

    private void LoadModeList()
    {
        ModeItems.Clear();

        ModeItems.Add(new ModeListItem
        {
            Id = "ask",
            Title = "Ask",

            IsDictationMode = false,
            IsSeparator = false,

        });

        ModeItems.Add(new ModeListItem
        {
            Id = "refine_auto",
            Title = "Refine (Auto)",

            IsDictationMode = false,
            IsSeparator = false,
        });

        ModeItems.Add(new ModeListItem
        {
            Id = "refine_instruction",
            Title = "Refine (Verbal)",

            IsDictationMode = false,
            IsSeparator = false,
        });

        ModeItems.Add(new ModeListItem
        {
            Id = "translate",
            Title = "Translate",

            IsDictationMode = false,
            IsSeparator = false,

        });

        ModeItems.Add(new ModeListItem
        {
            Id = "note",
            Title = "Notes",

            IsDictationMode = false,
            IsSeparator = false,

        });

        ModeItems.Add(new ModeListItem
        {
            Id = "chat",
            Title = "Chat",

            IsDictationMode = false,
            IsSeparator = false,

        });
    }

    // ── Model loading ──────────────────────────────────────────────────────

    private async Task LoadModelsAsync()
    {
        IsLoadingModels = true;

        try
        {
            var models = await _modelListService.GetAvailableModelsAsync().ConfigureAwait(false);

            // Populate on UI thread
            if (App.Current.MainWindow?.DispatcherQueue is { } dispatcher)
            {
                dispatcher.TryEnqueue(() =>
                {
                    PopulateModelLists(models);
                    IsLoadingModels = false;  // Set on UI thread
                });
            }
            else
            {
                // Fallback for unit tests
                PopulateModelLists(models);
                IsLoadingModels = false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ModesSettingsVM: Failed to load models");
            IsLoadingModels = false;
        }
    }

    private void PopulateModelLists(List<ModelInfo> models)
    {
        // Cloud models (no Ollama)
        CloudModelNames.Clear();
        _cloudModelIds.Clear();
        CloudModelNames.Add(_loc.GetString("Settings_Modes_ModelDefault_Cloud"));
        _cloudModelIds.Add("");

        var cloudModels = models.Where(m => !string.Equals(m.Provider, "Ollama (Local)", StringComparison.OrdinalIgnoreCase));
        foreach (var m in cloudModels)
        {
            CloudModelNames.Add($"{m.DisplayName}  ({m.Provider})");
            _cloudModelIds.Add(m.ModelId);
        }

        // Local models (Ollama only)
        LocalModelNames.Clear();
        _localModelIds.Clear();
        LocalModelNames.Add(_loc.GetString("Settings_Modes_ModelDefault_Local"));
        _localModelIds.Add("");

        var localModels = models.Where(m => string.Equals(m.Provider, "Ollama (Local)", StringComparison.OrdinalIgnoreCase));
        foreach (var m in localModels)
        {
            LocalModelNames.Add(m.DisplayName);
            _localModelIds.Add(m.ModelId);
        }
    }

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        await LoadModelsAsync();
    }

    // ── Selection change ───────────────────────────────────────────────────

    partial void OnSelectedIndexChanged(int value)
    {
        HasSelection = value >= 0 && value < ModeItems.Count;

        if (HasSelection)
        {
            SelectedModeTitle = ModeItems[value].Title;
            LoadDetail(ModeItems[value].Id);
        }
        else
        {
            SelectedModeTitle = "";
        }

        // Update visibility flags
        IsAskSelected = HasSelection && ModeItems[value].Id == "ask";
        IsNoteSelected = HasSelection && ModeItems[value].Id == "note";
        IsChatSelected = HasSelection && ModeItems[value].Id == "chat";

        // Notify computed property changes
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(ShowPromptFields));
    }

    private void LoadDetail(string pipelineType)
    {
        var pipeline = _pipelineManager.GetPipelineByType(pipelineType);
        if (pipeline is null)
        {
            Log.Warning("ModesSettingsVM: Pipeline '{Type}' not found", pipelineType);
            return;
        }

        // Common fields
        CloudSystemPrompt = pipeline.CloudProfile.SystemPrompt ?? "";
        LocalSystemPrompt = pipeline.LocalProfile.SystemPrompt ?? "";

        // Map model names to combo indices
        SelectedCloudModelIndex = FindModelIndex(_cloudModelIds, pipeline.CloudProfile.ModelName);
        SelectedLocalModelIndex = FindModelIndex(_localModelIds, pipeline.LocalProfile.ModelName);

        // Mode-specific fields
        switch (pipelineType)
        {
            case "ask":
                SelectedAskOutputIndex = (int)_settings.Current.General.AskOutput;
                break;

            case "note":
                var noteSettings = _settings.Current.Note;
                NoteFilePath = noteSettings.FilePath;
                NoteUseLlmProcessing = noteSettings.UseLlmProcessing;
                NoteTimestampFormat = noteSettings.TimestampFormat;
                UpdateNotePreview();
                break;

            case "chat":
                var chatSettings = _settings.Current.Chat;
                ChatFontSize = chatSettings.FontSize;
                ChatWindowOpacity = chatSettings.WindowOpacity;
                ChatForgetOnClose = chatSettings.ForgetOnClose;
                ChatMaxHistoryMessages = chatSettings.MaxHistoryMessages;
                ChatShowTimestamps = chatSettings.ShowTimestamps;
                ChatEnableMarkdown = chatSettings.EnableMarkdown;
                ChatAlwaysOnTop = chatSettings.AlwaysOnTop;
                ChatDefaultModelId = chatSettings.DefaultModelId ?? "";
                ChatDefaultSystemPrompt = chatSettings.DefaultSystemPrompt ?? "";
                ChatWebSearchEnabled = chatSettings.WebSearchEnabled;
                ChatSelectedThemeIndex = Array.IndexOf(ChatThemeCodes, chatSettings.Theme);
                if (ChatSelectedThemeIndex < 0) { ChatSelectedThemeIndex = 0; }
                break;
        }
    }

    private static int FindModelIndex(List<string> modelIds, string? targetId)
    {
        if (string.IsNullOrEmpty(targetId))
        {
            return 0; // "(Default)" entry
        }

        int index = modelIds.IndexOf(targetId);
        return index >= 0 ? index : 0;
    }

    // ── Save ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!HasSelection || SelectedIndex < 0 || SelectedIndex >= ModeItems.Count)
        {
            return;
        }

        string pipelineType = ModeItems[SelectedIndex].Id;

        try
        {
            // Save pipeline profiles (common to all modes)
            var cloudProfile = new UtilityProfile
            {
                SystemPrompt = string.IsNullOrWhiteSpace(CloudSystemPrompt) ? null : CloudSystemPrompt,
                ModelName = SelectedCloudModelIndex >= 0 && SelectedCloudModelIndex < _cloudModelIds.Count
                    ? _cloudModelIds[SelectedCloudModelIndex]
                    : null,
            };

            var localProfile = new UtilityProfile
            {
                SystemPrompt = string.IsNullOrWhiteSpace(LocalSystemPrompt) ? null : LocalSystemPrompt,
                ModelName = SelectedLocalModelIndex >= 0 && SelectedLocalModelIndex < _localModelIds.Count
                    ? _localModelIds[SelectedLocalModelIndex]
                    : null,
            };

            await _pipelineManager.UpdatePipelineAsync(pipelineType, cloudProfile, localProfile).ConfigureAwait(false);

            // Mode-specific settings
            switch (pipelineType)
            {
                case "ask":
                    var newGeneral = _settings.Current.General with { AskOutput = (AskOutputMode)SelectedAskOutputIndex };
                    var newSettingsAsk = _settings.Current with { General = newGeneral };
                    await _settings.UpdateAsync(newSettingsAsk).ConfigureAwait(false);
                    break;

                case "note":
                    var newNoteSettings = new NoteSettings
                    {
                        FilePath = NoteFilePath,
                        UseLlmProcessing = NoteUseLlmProcessing,
                        TimestampFormat = NoteTimestampFormat,
                    };
                    var newSettingsNote = _settings.Current with { Note = newNoteSettings };
                    await _settings.UpdateAsync(newSettingsNote).ConfigureAwait(false);
                    break;

                case "chat":
                    // Use 'with' to preserve unbound fields (WindowWidth, WindowHeight, etc.)
                    var newChatSettings = _settings.Current.Chat with
                    {
                        FontSize = ChatFontSize,
                        WindowOpacity = ChatWindowOpacity,
                        ForgetOnClose = ChatForgetOnClose,
                        MaxHistoryMessages = ChatMaxHistoryMessages,
                        ShowTimestamps = ChatShowTimestamps,
                        EnableMarkdown = ChatEnableMarkdown,
                        AlwaysOnTop = ChatAlwaysOnTop,
                        DefaultModelId = string.IsNullOrWhiteSpace(ChatDefaultModelId) ? null : ChatDefaultModelId,
                        DefaultSystemPrompt = string.IsNullOrWhiteSpace(ChatDefaultSystemPrompt) ? null : ChatDefaultSystemPrompt,
                        WebSearchEnabled = ChatWebSearchEnabled,
                        Theme = ChatSelectedThemeIndex >= 0 && ChatSelectedThemeIndex < ChatThemeCodes.Length
                            ? ChatThemeCodes[ChatSelectedThemeIndex]
                            : "System",
                    };
                    var newSettingsChat = _settings.Current with { Chat = newChatSettings };
                    await _settings.UpdateAsync(newSettingsChat).ConfigureAwait(false);
                    break;
            }

            Log.Information("ModesSettingsVM: Saved settings for {Type}", pipelineType);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ModesSettingsVM: Failed to save settings for {Type}", pipelineType);
        }
    }

    // ── Reset (Notes/Chat only) ────────────────────────────────────────────

    [RelayCommand]
    private void Reset()
    {
        if (!HasSelection || SelectedIndex < 0 || SelectedIndex >= ModeItems.Count)
        {
            return;
        }

        string pipelineType = ModeItems[SelectedIndex].Id;

        switch (pipelineType)
        {
            case "note":
                var noteDefaults = new NoteSettings();
                NoteFilePath = noteDefaults.FilePath;
                NoteUseLlmProcessing = noteDefaults.UseLlmProcessing;
                NoteTimestampFormat = noteDefaults.TimestampFormat;
                CloudSystemPrompt = PromptDefaults.Note;
                LocalSystemPrompt = PromptDefaults.Note;
                UpdateNotePreview();
                break;

            case "chat":
                var chatDefaults = new ChatSettings();
                ChatFontSize = chatDefaults.FontSize;
                ChatWindowOpacity = chatDefaults.WindowOpacity;
                ChatForgetOnClose = chatDefaults.ForgetOnClose;
                ChatMaxHistoryMessages = chatDefaults.MaxHistoryMessages;
                ChatShowTimestamps = chatDefaults.ShowTimestamps;
                ChatEnableMarkdown = chatDefaults.EnableMarkdown;
                ChatAlwaysOnTop = chatDefaults.AlwaysOnTop;
                ChatDefaultModelId = chatDefaults.DefaultModelId ?? "";
                ChatDefaultSystemPrompt = chatDefaults.DefaultSystemPrompt ?? "";
                ChatWebSearchEnabled = chatDefaults.WebSearchEnabled;
                ChatSelectedThemeIndex = Array.IndexOf(ChatThemeCodes, chatDefaults.Theme);
                CloudSystemPrompt = PromptDefaults.Chat;
                LocalSystemPrompt = PromptDefaults.Chat;
                break;
        }
    }

    // ── Notes — Browse file path ───────────────────────────────────────────

    [RelayCommand]
    private async Task BrowseNoteFilePathAsync()
    {
        var picker = new FileSavePicker();
        picker.SuggestedFileName = Path.GetFileName(NoteFilePath);
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
            NoteFilePath = file.Path;
        }
    }

    // ── Notes — Live preview ───────────────────────────────────────────────

    partial void OnNoteTimestampFormatChanged(string value)
    {
        UpdateNotePreview();
    }

    private void UpdateNotePreview()
    {
        try
        {
            string timestamp = DateTime.Now.ToString(NoteTimestampFormat, CultureInfo.InvariantCulture);
            NotePreviewText = $"[{timestamp}]\nSample transcription...\n---";
        }
        catch (FormatException)
        {
            NotePreviewText = "[Invalid format]\nSample transcription...\n---";
        }
    }
}
