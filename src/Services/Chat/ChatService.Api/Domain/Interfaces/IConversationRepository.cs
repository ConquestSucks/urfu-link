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
}
