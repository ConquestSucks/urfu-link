namespace Urfu.Link.Services.Chat.Domain.Interfaces;

public enum CursorDirection
{
    Older = 0,
    Newer = 1,
}

public readonly record struct ConversationCursor(DateTimeOffset LastMessageAtUtc, string ConversationId);

public readonly record struct MessageCursor(DateTimeOffset CreatedAtUtc, Guid MessageId);

public readonly record struct ThreadActivityCursor(DateTimeOffset LastActivityAtUtc, Guid RootMessageId);
