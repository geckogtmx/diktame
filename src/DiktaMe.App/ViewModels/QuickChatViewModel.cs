
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiktaMe.Core.Config;
using DiktaMe.Core.Data;
using DiktaMe.Core.Input;
using DiktaMe.Core.LLM;
using DiktaMe.Core.Pipeline;
using DiktaMe.Core.TTS;
using Microsoft.UI.Dispatching;
using Serilog;

namespace DiktaMe.App.ViewModels;
public sealed partial class QuickChatViewModel : ObservableObject
{
    private const string DefaultSystemPrompt = "You are a helpful assistant. Answer concisely.";

    private readonly PipelineFactory _pipelineFactory;
    private readonly ConversationManager _conversationManager;
    private readonly ModelListService _modelListService;
    private readonly SettingsManager _settings;
    private readonly TtsSpeaker _ttsSpeaker;
    private readonly DispatcherQueue _dispatcher;

    private string? _currentConversationId;
    private int _userMessageCount;
    private int _totalTokens;
    private string _lastSentMessage = "";

    [ObservableProperty] private string _inputText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    /// <summary>Inverse of IsBusy for XAML binding (avoids converter in Window).</summary>
    public bool IsNotBusy => !IsBusy;

    [ObservableProperty] private string _conversationTitle = "New Chat";
    [ObservableProperty] private string _systemPrompt = DefaultSystemPrompt;
    [ObservableProperty] private string? _selectedModelId;
    [ObservableProperty] private bool _isSystemPromptVisible;
    [ObservableProperty] private string _tokenCountText = "";
    [ObservableProperty] private bool _webSearchEnabled;

    // ── Image attachment (Vision → Chat flow) ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImageAttachment))]
    private byte[]? _imageAttachment;

    [ObservableProperty]
    private string? _imageAttachmentMimeType;

    /// <summary>True when a Vision image is attached and pending send with the next message.</summary>
    public bool HasImageAttachment => ImageAttachment is { Length: > 0 };

    /// <summary>
    /// Model ID to pre-select once models finish loading.
    /// Set by the Vision modal's Local/Cloud toggle.
    /// </summary>
    public string? PendingModelId { get; set; }

    /// <summary>
    /// When true, auto-sends the first message once models are loaded and InputText is set.
    /// Used when the Vision modal has a pre-filled query that should fire immediately.
    /// </summary>
    public bool AutoSendOnReady { get; set; }

    /// <summary>
    /// Sets image context from the Vision modal.
    /// The image will be included with the user's first chat message.
    /// </summary>
    public void SetImageContext(byte[] imageData, string mimeType)
    {
        ImageAttachment = imageData;
        ImageAttachmentMimeType = mimeType;
    }

    /// <summary>Clears the attached image.</summary>
    public void ClearImageAttachment()
    {
        ImageAttachment = null;
        ImageAttachmentMimeType = null;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClipboardAttachment))]
    [NotifyPropertyChangedFor(nameof(ClipboardPreview))]
    private string? _clipboardAttachment;

    /// <summary>True when clipboard text is attached and pending send.</summary>
    public bool HasClipboardAttachment => !string.IsNullOrEmpty(ClipboardAttachment);

    /// <summary>Short preview of attached clipboard text for the UI badge.</summary>
    public string ClipboardPreview =>
        ClipboardAttachment is { Length: > 40 }
            ? string.Concat(ClipboardAttachment.AsSpan(0, 40), "...")
            : ClipboardAttachment ?? "";

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<ModelInfo> AvailableModels { get; } = [];
    public ObservableCollection<ConversationRecord> RecentConversations { get; } = [];

    public QuickChatViewModel(
        PipelineFactory pipelineFactory,
        ConversationManager conversationManager,
        ModelListService modelListService,
        SettingsManager settings,
        TtsSpeaker ttsSpeaker)
    {
        _pipelineFactory = pipelineFactory;
        _conversationManager = conversationManager;
        _modelListService = modelListService;
        _settings = settings;
        _ttsSpeaker = ttsSpeaker;
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        // Apply default from settings
        var chatSettings = _settings.Current.Chat;
        _selectedModelId = chatSettings.DefaultModelId;
        _webSearchEnabled = chatSettings.WebSearchEnabled;
        if (!string.IsNullOrWhiteSpace(chatSettings.DefaultSystemPrompt))
        {
            _systemPrompt = chatSettings.DefaultSystemPrompt;
        }
    }

    /// <summary>Loads available models from all configured providers.</summary>
    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        try
        {
            var models = await _modelListService.GetAvailableModelsAsync();
            _dispatcher.TryEnqueue(() =>
            {
                AvailableModels.Clear();
                foreach (var model in models)
                {
                    AvailableModels.Add(model);
                }

                // Apply pre-selected model from Vision modal if set
                if (PendingModelId is not null)
                {
                    SelectedModelId = PendingModelId;
                    PendingModelId = null;
                }

                // Auto-send the pre-filled query from Vision modal
                if (AutoSendOnReady && !string.IsNullOrWhiteSpace(InputText))
                {
                    AutoSendOnReady = false;
                    _ = SendAsync();
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "QuickChatVM: failed to load models");
        }
    }

    /// <summary>Loads recent conversations for the history flyout.</summary>
    [RelayCommand]
    private async Task LoadRecentConversationsAsync()
    {
        try
        {
            var recent = await _conversationManager.GetRecentAsync(50);
            _dispatcher.TryEnqueue(() =>
            {
                RecentConversations.Clear();
                foreach (var conv in recent)
                {
                    RecentConversations.Add(conv);
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "QuickChatVM: failed to load recent conversations");
        }
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        string question = InputText.Trim();
        if (string.IsNullOrEmpty(question) || IsBusy)
        {
            return;
        }

        // Ensure conversation exists
        if (_currentConversationId is null)
        {
            await CreateNewConversationInternalAsync();
        }

        // Prepend clipboard context if attached
        string messageForLlm = question;
        if (!string.IsNullOrEmpty(ClipboardAttachment))
        {
            messageForLlm = $"[Clipboard context]\n{ClipboardAttachment}\n[/Clipboard context]\n\n{question}";
            ClipboardAttachment = null;
        }

        // Add user message to UI (show the original question, not the context-augmented version)
        var userMsg = new ChatMessageViewModel(question, isUser: true);
        Messages.Add(userMsg);
        _lastSentMessage = question;
        InputText = "";
        IsBusy = true;

        try
        {
            // Persist user message
            await _conversationManager.AddMessageAsync(
                _currentConversationId!, "user", question);

            // Set fallback title from first user message (auto-title overwrites later)
            if (_userMessageCount == 0 && string.Equals(ConversationTitle, "New Chat", StringComparison.Ordinal))
            {
                string fallbackTitle = question.Length > 40
                    ? string.Concat(question.AsSpan(0, 40), "...")
                    : question;
                ConversationTitle = fallbackTitle;
                _ = _conversationManager.UpdateTitleAsync(_currentConversationId!, fallbackTitle);
            }

            // Build conversation history for multi-turn
            // Pass messageForLlm to override the last user message with clipboard context if attached
            var history = BuildConversationHistory(messageForLlm);

            // Get context window for the selected model
            int contextWindow = GetContextWindowForModel(SelectedModelId);

            // When an image is attached, resolve the correct provider for the selected model.
            // CreateChatPipeline() returns the dictation LLM (Ollama text-only by default),
            // which can't handle images if the user selected a cloud model like Gemini.
            bool hasImage = history.Any(t => t.ImageData is { Length: > 0 });
            ChatPipeline pipeline;
            if (hasImage && !string.IsNullOrWhiteSpace(SelectedModelId))
            {
                string provider = ModelListService.ResolveProviderFromModelId(SelectedModelId);
                pipeline = _pipelineFactory.CreateChatPipelineForModel(provider, SelectedModelId);
            }
            else
            {
                pipeline = _pipelineFactory.CreateChatPipeline();
            }

            var options = new ChatOptions
            {
                SystemPrompt = SystemPrompt,
                ModelName = SelectedModelId,
                WebSearchEnabled = WebSearchEnabled,
            };

            var result = await pipeline.RunConversationAsync(
                options, history, contextWindow);

            // Clear image attachment after first send (image stays in conversation history)
            if (ImageAttachment is not null)
            {
                ClearImageAttachment();
            }

            _dispatcher.TryEnqueue(() =>
            {
                if (result.IsSuccess)
                {
                    int outputTokens = result.OutputTokens ?? (result.Text.Length / 4);
                    var assistantMsg = new ChatMessageViewModel(
                        result.Text, isUser: false, tokenCount: outputTokens);
                    Messages.Add(assistantMsg);

                    // Update running token total
                    int inputTokens = result.InputTokens ?? (messageForLlm.Length / 4);
                    _totalTokens += inputTokens + outputTokens;
                    TokenCountText = $"~{_totalTokens:N0} tokens";

                    // Persist assistant message (fire-and-forget)
                    _ = _conversationManager.AddMessageAsync(
                        _currentConversationId!, "assistant", result.Text);

                    _userMessageCount++;

                    // Auto-title after 2nd user message gets a response
                    if (_userMessageCount == 2 && string.Equals(ConversationTitle, "New Chat", StringComparison.Ordinal))
                    {
                        _ = GenerateAutoTitleAsync();
                    }
                }
                else
                {
                    Messages.Add(new ChatMessageViewModel(
                        $"Error: {result.ErrorMessage}", isUser: false));
                }
            });

            // TTS: speak assistant response if enabled (SPEC_003 Phase D)
            if (result.IsSuccess)
            {
                long ttsMs = await _ttsSpeaker.SpeakIfEnabledAsync(result.Text, "chat");
                if (ttsMs > 0)
                {
                    Log.Information("Chat: TTS played in {TtsMs}ms", ttsMs);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Quick chat failed");
            _dispatcher.TryEnqueue(() =>
            {
                Messages.Add(new ChatMessageViewModel(
                    $"Error: {ex.Message}", isUser: false));
            });
        }
        finally
        {
            _dispatcher.TryEnqueue(() => IsBusy = false);
        }
    }

    [RelayCommand]
    private async Task NewConversationAsync()
    {
        Messages.Clear();
        InputText = "";
        _userMessageCount = 0;
        _totalTokens = 0;
        TokenCountText = "";
        ConversationTitle = "New Chat";

        // Reset system prompt to default
        var chatSettings = _settings.Current.Chat;
        SystemPrompt = !string.IsNullOrWhiteSpace(chatSettings.DefaultSystemPrompt)
            ? chatSettings.DefaultSystemPrompt
            : DefaultSystemPrompt;

        await CreateNewConversationInternalAsync();
    }

    [RelayCommand]
    private async Task LoadConversationAsync(string conversationId)
    {
        try
        {
            var conv = await _conversationManager.GetAsync(conversationId);
            if (conv is null)
            {
                return;
            }

            var dbMessages = await _conversationManager.GetMessagesAsync(conversationId);

            _dispatcher.TryEnqueue(() =>
            {
                Messages.Clear();
                _currentConversationId = conversationId;
                ConversationTitle = conv.Title ?? "Untitled";
                SystemPrompt = conv.SystemPrompt;
                SelectedModelId = conv.ModelId;
                _userMessageCount = dbMessages.Count(m =>
                    string.Equals(m.Role, "user", StringComparison.Ordinal));

                _totalTokens = 0;
                foreach (var msg in dbMessages)
                {
                    int msgTokens = msg.Tokens ?? (msg.Content.Length / 4);
                    _totalTokens += msgTokens;

                    Messages.Add(new ChatMessageViewModel(
                        msg.Content,
                        isUser: string.Equals(msg.Role, "user", StringComparison.Ordinal),
                        timestamp: msg.CreatedAt,
                        tokenCount: msg.Tokens,
                        id: msg.Id));
                }

                TokenCountText = _totalTokens > 0 ? $"~{_totalTokens:N0} tokens" : "";
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "QuickChatVM: failed to load conversation {Id}", conversationId);
        }
    }

    [RelayCommand]
    private async Task DeleteConversationAsync(string conversationId)
    {
        await _conversationManager.DeleteAsync(conversationId);

        if (string.Equals(_currentConversationId, conversationId, StringComparison.Ordinal))
        {
            await NewConversationAsync();
        }
    }

    [RelayCommand]
    private static void CopyMessage(string text)
    {
        try
        {
            var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "QuickChatVM: clipboard copy failed");
        }
    }

    [RelayCommand]
    private void ToggleSystemPrompt()
    {
        IsSystemPromptVisible = !IsSystemPromptVisible;
    }

    partial void OnWebSearchEnabledChanged(bool value)
    {
        var updated = _settings.Current with { Chat = _settings.Current.Chat with { WebSearchEnabled = value } };
        _ = _settings.UpdateAsync(updated);
    }

    [RelayCommand]
    private void ClearMessages()
    {
        Messages.Clear();
        _userMessageCount = 0;
        _totalTokens = 0;
        TokenCountText = "";
    }

    /// <summary>Recalls the last sent message into the input box (Up arrow in empty input).</summary>
    public void RecallLastMessage()
    {
        if (string.IsNullOrEmpty(InputText) && !string.IsNullOrEmpty(_lastSentMessage))
        {
            InputText = _lastSentMessage;
        }
    }

    [RelayCommand]
    private void AttachClipboard()
    {
        try
        {
            string clipText = ClipboardManager.GetText();
            if (string.IsNullOrWhiteSpace(clipText))
            {
                Log.Information("QuickChatVM: clipboard is empty, nothing to attach");
                return;
            }

            ClipboardAttachment = clipText;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "QuickChatVM: failed to read clipboard");
        }
    }

    [RelayCommand]
    private void ClearClipboardAttachment()
    {
        ClipboardAttachment = null;
    }

    /// <summary>Generates a markdown representation of the current conversation.</summary>
    public string ExportAsMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {ConversationTitle}");
        sb.AppendLine();

        foreach (var msg in Messages)
        {
            sb.AppendLine(msg.IsUser ? "## User" : "## Assistant");
            sb.AppendLine();
            sb.AppendLine(msg.Text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private async Task CreateNewConversationInternalAsync()
    {
        try
        {
            var conv = await _conversationManager.CreateAsync(
                SystemPrompt, SelectedModelId);
            _currentConversationId = conv.Id;
            _userMessageCount = 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "QuickChatVM: failed to create conversation");
        }
    }

    private List<ConversationTurn> BuildConversationHistory(string? lastUserMessageOverride = null)
    {
        var history = new List<ConversationTurn>(Messages.Count);
        bool imageConsumed = false;

        for (int i = 0; i < Messages.Count; i++)
        {
            var msg = Messages[i];
            string content = msg.Text;

            // Override the last message's content if it's a user message
            // (used to inject clipboard context without changing the UI text)
            if (lastUserMessageOverride is not null && i == Messages.Count - 1 && msg.IsUser)
            {
                content = lastUserMessageOverride;
            }

            // Attach image to the first user message that carries it
            byte[]? imgData = null;
            string? imgMime = null;
            if (!imageConsumed && msg.IsUser && ImageAttachment is { Length: > 0 })
            {
                imgData = ImageAttachment;
                imgMime = ImageAttachmentMimeType;
                imageConsumed = true;
            }

            history.Add(new ConversationTurn(
                msg.IsUser ? "user" : "assistant",
                content,
                imgData,
                imgMime));
        }

        return history;
    }

    private int GetContextWindowForModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return 8192;
        }

        foreach (var model in AvailableModels)
        {
            if (string.Equals(model.ModelId, modelId, StringComparison.Ordinal)
                && model.ContextWindow.HasValue)
            {
                return model.ContextWindow.Value;
            }
        }

        return 8192;
    }

    private async Task GenerateAutoTitleAsync()
    {
        try
        {
            // Build a short summary from the first 2 exchanges for title generation
            var titleHistory = Messages
                .Take(4)
                .Select(m => new ConversationTurn(
                    m.IsUser ? "user" : "assistant", m.Text))
                .ToList();

            var pipeline = _pipelineFactory.CreateChatPipeline();
            var options = new ChatOptions
            {
                SystemPrompt = "Generate a 3-5 word title for this conversation. Output only the title, nothing else.",
                ModelName = SelectedModelId,
            };

            var result = await pipeline.RunConversationAsync(options, titleHistory);
            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Text))
            {
                string title = result.Text.Trim().Trim('"', '\'');
                if (title.Length > 50)
                {
                    title = title[..50];
                }

                await _conversationManager.UpdateTitleAsync(_currentConversationId!, title);
                _dispatcher.TryEnqueue(() => ConversationTitle = title);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "QuickChatVM: auto-title generation failed");
        }
    }
}
