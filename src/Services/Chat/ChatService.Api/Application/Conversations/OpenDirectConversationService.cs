using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Conversations;

/// <summary>
/// Opens a direct conversation between the caller and a peer. Idempotent by virtue of the
/// deterministic conversation Id derived from the sorted user pair: a second call with the
/// same participants returns the existing conversation and does not republish the creation
/// event nor re-broadcast realtime updates.
/// </summary>
public sealed class OpenDirectConversationService(
    IConversationRepository repository,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task<Conversation> OpenAsync(Guid callerUserId, Guid peerUserId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var draft = Conversation.OpenDirect(callerUserId, peerUserId, now);

        var existing = await repository.GetByIdAsync(draft.Id, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        await repository.UpsertAsync(draft, cancellationToken).ConfigureAwait(false);
        await dispatcher.PublishAsync(
            new ChatConversationCreatedEvent(
                draft.Id,
                draft.Type,
                draft.Participants,
                now),
            cancellationToken).ConfigureAwait(false);

        await broadcaster.NotifyConversationUpdatedAsync(
            draft.Participants,
            ConversationDto.FromDomain(draft),
            cancellationToken).ConfigureAwait(false);

        return draft;
    }
}
