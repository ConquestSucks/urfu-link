using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Domain.Interfaces;

public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns messages whose ids are in <paramref name="messageIds"/> and that belong to
    /// <paramref name="conversationId"/>. Useful for forward and pinned-list materialization
    /// where ids alone could otherwise leak across conversations.
    /// </summary>
    Task<IReadOnlyList<Message>> GetByIdsAsync(
        string conversationId,
        IReadOnlyList<Guid> messageIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Bulk-loads messages by id across any conversation. Used by forward, where source
    /// messages may live in different conversations and the caller verifies membership for
    /// each source separately. Result order is unspecified — callers should index by id.
    /// </summary>
    Task<IReadOnlyList<Message>> GetManyAsync(
        IReadOnlyList<Guid> messageIds,
        CancellationToken cancellationToken);

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
    /// as Read. Returns the ids that actually transitioned, ordered from oldest to newest
    /// (so the last element is the new anchor). Returns an empty list if nothing transitioned.
    /// </summary>
    Task<IReadOnlyList<Guid>> MarkReadUpToAsync(
        string conversationId,
        Guid upToMessageId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically applies an edit: sets new body / mentions / editedAt and appends a single
    /// history entry. Returns false if the message does not exist or is in the Deleted state.
    /// </summary>
    Task<bool> ApplyEditAsync(
        Guid messageId,
        string newBody,
        IReadOnlyList<Guid> mentions,
        EditHistoryEntry historyEntry,
        DateTimeOffset editedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically applies a tombstone: clears body/attachments/reactions/mentions/replyTo/
    /// forwardedFrom, sets state to Deleted and records the deleter. Returns false if the
    /// message is missing or already deleted.
    /// </summary>
    Task<bool> ApplyDeleteForEveryoneAsync(
        Guid messageId,
        Guid byUserId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds <paramref name="userId"/> to the message's <c>hiddenFor</c> set. Idempotent.
    /// Returns true when the user was newly added.
    /// </summary>
    Task<bool> AddHiddenForAsync(Guid messageId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Adds or replaces a reaction by <c>userId</c>. Pull-then-push semantics: any prior
    /// reaction by the same user is removed before the new one is appended. Returns true if
    /// the message exists and the reaction was applied.
    /// </summary>
    Task<bool> AddReactionAsync(Guid messageId, Reaction reaction, CancellationToken cancellationToken);

    Task<bool> RemoveReactionAsync(
        Guid messageId,
        Guid userId,
        string emoji,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds a <see cref="ReadReceipt"/>, deduplicated by user id. Returns true when the receipt
    /// was newly recorded.
    /// </summary>
    Task<bool> AddReadByAsync(Guid messageId, ReadReceipt receipt, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReadReceipt>> GetReadReceiptsAsync(Guid messageId, CancellationToken cancellationToken);
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
