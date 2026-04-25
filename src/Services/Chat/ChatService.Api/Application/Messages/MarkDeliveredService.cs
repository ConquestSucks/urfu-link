using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record MarkDeliveredRequest(string ConversationId, Guid RecipientUserId, IReadOnlyList<Guid> MessageIds);

public sealed class MarkDeliveredService(
    IConversationRepository conversations,
    IMessageRepository messages,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<Guid>> MarkAsync(MarkDeliveredRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = await conversations.GetByIdAsync(request.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(request.ConversationId);

        if (!conversation.IsParticipant(request.RecipientUserId))
        {
            throw new ChatAccessDeniedException(request.ConversationId, request.RecipientUserId);
        }

        if (request.MessageIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var now = clock.GetUtcNow();
        var transitioned = await messages.MarkDeliveredAsync(
            conversation.Id, request.MessageIds, now, cancellationToken).ConfigureAwait(false);

        foreach (var id in transitioned)
        {
            await dispatcher.PublishAsync(
                new ChatMessageDeliveredEvent(conversation.Id, id, request.RecipientUserId, now),
                cancellationToken).ConfigureAwait(false);
        }

        if (transitioned.Count > 0)
        {
            // Notify the OTHER participants (typically the sender) that their messages are now delivered.
            var observers = conversation.Participants.Where(p => p != request.RecipientUserId).ToList();
            await broadcaster.NotifyMessageDeliveredAsync(
                observers, conversation.Id, transitioned, request.RecipientUserId, cancellationToken).ConfigureAwait(false);
        }

        return transitioned;
    }
}
