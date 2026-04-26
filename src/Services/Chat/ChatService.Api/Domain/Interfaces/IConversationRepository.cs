using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Domain.Interfaces;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(string conversationId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the conversation if no document with the same Id exists yet. Returns
    /// <see langword="true"/> when this caller created it, <see langword="false"/> when a
    /// concurrent caller had already inserted a conversation with the same Id (no exception is
    /// thrown — the caller is expected to fetch the existing one).
    /// </summary>
    Task<bool> TryCreateAsync(Conversation conversation, CancellationToken cancellationToken);

    Task UpdateLastMessageAsync(
        string conversationId,
        MessagePreview preview,
        DateTimeOffset lastMessageAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Conversation>> ListByParticipantAsync(
        Guid userId,
        ConversationCursor? cursor,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds <paramref name="messageId"/> to <c>pinnedMessageIds</c> using <c>$addToSet</c> with
    /// a precondition that the array length is below <paramref name="maxPinned"/>. Returns
    /// false when the conversation is missing, the message is already pinned, or the cap is
    /// already reached.
    /// </summary>
    Task<bool> AddPinnedMessageAsync(
        string conversationId,
        Guid messageId,
        int maxPinned,
        CancellationToken cancellationToken);

    Task<bool> RemovePinnedMessageAsync(
        string conversationId,
        Guid messageId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the ids of every conversation in which <paramref name="userId"/> is a participant.
    /// No pagination — the full list is required by callers (e.g. global message search) that
    /// then use it as an <c>$in</c> filter against the messages collection.
    /// </summary>
    Task<IReadOnlyList<string>> GetUserConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Bulk-loads conversations by id. Returns only conversations that exist; missing ids are
    /// silently skipped. Used by search to hydrate <c>conversationPreview</c> per result without
    /// an N+1.
    /// </summary>
    Task<IReadOnlyList<Conversation>> GetByIdsAsync(
        IReadOnlyList<string> conversationIds,
        CancellationToken cancellationToken);
}
