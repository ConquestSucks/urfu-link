namespace Urfu.Link.Services.Chat.Application;

public sealed class ChatAccessDeniedException : InvalidOperationException
{
    public ChatAccessDeniedException()
    {
    }

    public ChatAccessDeniedException(string message)
        : base(message)
    {
    }

    public ChatAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ChatAccessDeniedException(string conversationId, Guid userId)
        : base($"User '{userId:D}' is not a participant of conversation '{conversationId}'.")
    {
        ConversationId = conversationId;
        UserId = userId;
    }

    public string ConversationId { get; } = string.Empty;

    public Guid UserId { get; }
}

public sealed class ConversationNotFoundException : InvalidOperationException
{
    public ConversationNotFoundException()
    {
    }

    public ConversationNotFoundException(string message)
        : base(message)
    {
    }

    public ConversationNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static ConversationNotFoundException For(string conversationId)
        => new($"Conversation '{conversationId}' was not found.");
}

public sealed class ChatMessageNotFoundException : InvalidOperationException
{
    public ChatMessageNotFoundException()
    {
    }

    public ChatMessageNotFoundException(string message)
        : base(message)
    {
    }

    public ChatMessageNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static ChatMessageNotFoundException For(Guid messageId)
        => new($"Message '{messageId:D}' was not found.");
}

public sealed class ChatPinLimitExceededException : InvalidOperationException
{
    public ChatPinLimitExceededException()
    {
    }

    public ChatPinLimitExceededException(string message)
        : base(message)
    {
    }

    public ChatPinLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ChatPinLimitExceededException(string conversationId, int maxPinned)
        : base($"Conversation '{conversationId}' already has the maximum of {maxPinned} pinned messages.")
    {
        ConversationId = conversationId;
        MaxPinned = maxPinned;
    }

    public string ConversationId { get; } = string.Empty;

    public int MaxPinned { get; }
}

public sealed class ChatEditTtlExpiredException : InvalidOperationException
{
    public ChatEditTtlExpiredException()
    {
    }

    public ChatEditTtlExpiredException(string message)
        : base(message)
    {
    }

    public ChatEditTtlExpiredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static ChatEditTtlExpiredException For(Guid messageId, TimeSpan ttl)
        => new($"Edit window of {ttl} has expired for message '{messageId:D}'.");
}

public sealed class ChatNotMessageAuthorException : InvalidOperationException
{
    public ChatNotMessageAuthorException()
    {
    }

    public ChatNotMessageAuthorException(string message)
        : base(message)
    {
    }

    public ChatNotMessageAuthorException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ChatNotMessageAuthorException(Guid messageId, Guid userId)
        : base($"User '{userId:D}' is not the author of message '{messageId:D}'.")
    {
        MessageId = messageId;
        UserId = userId;
    }

    public Guid MessageId { get; }

    public Guid UserId { get; }
}

public sealed class ChatReplyTargetNotFoundException : InvalidOperationException
{
    public ChatReplyTargetNotFoundException()
    {
    }

    public ChatReplyTargetNotFoundException(string message)
        : base(message)
    {
    }

    public ChatReplyTargetNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ChatReplyTargetNotFoundException(Guid messageId, string conversationId)
        : base($"Reply target message '{messageId:D}' was not found in conversation '{conversationId}'.")
    {
        MessageId = messageId;
        ConversationId = conversationId;
    }

    public Guid MessageId { get; }

    public string ConversationId { get; } = string.Empty;
}

public sealed class ChatForwardLimitExceededException : InvalidOperationException
{
    public ChatForwardLimitExceededException()
    {
    }

    public ChatForwardLimitExceededException(string message)
        : base(message)
    {
    }

    public ChatForwardLimitExceededException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ChatForwardLimitExceededException(int requested, int maxAllowed)
        : base($"Forward request of {requested} messages exceeds the maximum of {maxAllowed}.")
    {
        Requested = requested;
        MaxAllowed = maxAllowed;
    }

    public int Requested { get; }

    public int MaxAllowed { get; }
}

public sealed class ChatReactionNotAllowedException : InvalidOperationException
{
    public ChatReactionNotAllowedException()
    {
    }

    public ChatReactionNotAllowedException(string message)
        : base(message)
    {
    }

    public ChatReactionNotAllowedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static ChatReactionNotAllowedException ForEmoji(string emoji)
        => new($"Reaction emoji '{emoji}' is not allowed.") { Emoji = emoji };

    public string Emoji { get; private init; } = string.Empty;
}
