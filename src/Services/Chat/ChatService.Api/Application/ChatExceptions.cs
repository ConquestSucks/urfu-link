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
