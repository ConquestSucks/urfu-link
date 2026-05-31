using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
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
        ConversationListFilter filter,
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

    /// <summary>
    /// Returns the conversation backed by <paramref name="disciplineId"/>, or
    /// <see langword="null"/> when there is none yet. Used by the discipline-event consumer
    /// to keep participant sync idempotent.
    /// </summary>
    Task<Conversation?> GetByDisciplineIdAsync(Guid disciplineId, CancellationToken cancellationToken);

    Task<Conversation?> GetGeneralDisciplineAsync(Guid disciplineId, CancellationToken cancellationToken);

    Task<Conversation?> GetByDisciplineSubgroupIdAsync(
        Guid disciplineId,
        Guid subgroupId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Conversation>> ListByDisciplineIdAsync(
        Guid disciplineId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Adds <paramref name="userId"/> with <paramref name="role"/> to the conversation. Returns
    /// <see langword="true"/> when the user was added (so the caller knows when to publish a
    /// derived event), <see langword="false"/> when already present.
    /// </summary>
    Task<bool> AddParticipantAsync(
        string conversationId,
        Guid userId,
        ParticipantRole role,
        CancellationToken cancellationToken);

    Task<bool> EnsureParticipantAsync(
        string conversationId,
        Guid userId,
        ParticipantRole role,
        CancellationToken cancellationToken);

    Task<bool> RemoveParticipantAsync(
        string conversationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> ChangeParticipantRoleAsync(
        string conversationId,
        Guid userId,
        ParticipantRole newRole,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks the conversation as archived. Idempotent — second call is a no-op and returns
    /// <see langword="false"/>.
    /// </summary>
    Task<bool> ArchiveAsync(
        string conversationId,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken);

    Task<bool> UnarchiveAsync(
        string conversationId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes display metadata (title and cover) projected from the source aggregate. Used
    /// by the discipline.updated.v1 handler. Returns <see langword="true"/> when at least one
    /// document was modified.
    /// </summary>
    Task<bool> UpdateMetadataAsync(
        string conversationId,
        string? title,
        Guid? coverAssetId,
        CancellationToken cancellationToken);

    Task<bool> UpdateDisciplineMetadataAsync(
        Guid disciplineId,
        string? disciplineTitle,
        Guid? coverAssetId,
        CancellationToken cancellationToken);

    Task<bool> UpdateSubgroupMetadataAsync(
        string conversationId,
        string subgroupName,
        CancellationToken cancellationToken);
}
