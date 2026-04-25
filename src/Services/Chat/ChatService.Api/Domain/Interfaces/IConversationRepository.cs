using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.Domain.Interfaces;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(string conversationId, CancellationToken cancellationToken);

    Task UpsertAsync(Conversation conversation, CancellationToken cancellationToken);

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
