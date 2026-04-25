using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Domain.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a new message. Throws <see cref="DuplicateClientMessageException"/> when the
    /// (senderId, clientMessageId) pair already exists in the collection.
    /// </summary>
    Task InsertAsync(Message message, CancellationToken cancellationToken);

    Task<Message?> FindByClientMessageIdAsync(
        Guid senderId,
        string clientMessageId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Message>> ListByConversationAsync(
        string conversationId,
        MessageCursor? cursor,
        int limit,
        CursorDirection direction,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically transitions matching messages from Sent to Delivered. Returns the IDs that
    /// were actually transitioned (already-delivered messages are ignored).
    /// </summary>
    Task<IReadOnlyList<Guid>> MarkDeliveredAsync(
        string conversationId,
        IReadOnlyList<Guid> messageIds,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks all messages in the conversation up to and including <paramref name="upToMessageId"/>
    /// as Read. Returns the actual highest <see cref="MessageState.Read"/> message id (or
    /// <see langword="null"/> if no transition occurred).
    /// </summary>
    Task<Guid?> MarkReadUpToAsync(
        string conversationId,
        Guid upToMessageId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken);
}

public sealed class DuplicateClientMessageException : InvalidOperationException
{
    public DuplicateClientMessageException()
    {
    }

    public DuplicateClientMessageException(string message)
        : base(message)
    {
    }

    public DuplicateClientMessageException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public DuplicateClientMessageException(Guid senderId, string clientMessageId)
        : base($"Duplicate clientMessageId '{clientMessageId}' for sender '{senderId:N}'.")
    {
        SenderId = senderId;
        ClientMessageId = clientMessageId;
    }

    public Guid SenderId { get; }

    public string ClientMessageId { get; } = string.Empty;
}
