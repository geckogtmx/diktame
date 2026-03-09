
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace DiktaMe.App.ViewModels;

/// <summary>
/// ViewModel for a single chat message displayed in the Quick Chat window.
/// Exposes properties for UI binding (text, role, timestamp, copy support).
/// </summary>
public sealed partial class ChatMessageViewModel : ObservableObject
{
    /// <summary>Database message ID (0 for unsaved/in-memory messages).</summary>
    public long Id { get; init; }

    /// <summary>The message text content.</summary>
    [ObservableProperty] private string _text;

    /// <summary>True for user messages, false for assistant messages.</summary>
    public bool IsUser { get; init; }

    /// <summary>Visibility for user message bubble (avoids converter in DataTemplate).</summary>
    public Visibility UserVisibility => IsUser ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Visibility for assistant message bubble (avoids converter in DataTemplate).</summary>
    public Visibility AssistantVisibility => IsUser ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>When the message was created.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>Approximate token count (chars / 4), populated from LlmResult when available.</summary>
    public int? TokenCount { get; init; }

    public ChatMessageViewModel(string text, bool isUser, DateTimeOffset? timestamp = null, int? tokenCount = null, long id = 0)
    {
        _text = text;
        IsUser = isUser;
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
        TokenCount = tokenCount;
        Id = id;
    }
}
