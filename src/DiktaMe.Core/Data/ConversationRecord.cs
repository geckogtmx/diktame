
namespace DiktaMe.Core.Data;

/// <summary>
/// Represents a persisted chat conversation with metadata.
/// </summary>
public sealed record ConversationRecord(
    string Id,
    string? Title,
    string SystemPrompt,
    string? ModelId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MessageCount);

/// <summary>
/// Represents a single message within a conversation.
/// </summary>
public sealed record ConversationMessageRecord(
    long Id,
    string ConversationId,
    string Role,
    string Content,
    int? Tokens,
    DateTimeOffset CreatedAt);
