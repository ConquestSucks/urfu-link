using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record MarkReadRequest(string ConversationId, Guid ReaderUserId, Guid UpToMessageId);

public sealed class MarkReadService(
    IConversationRepository conversations,
    IMessageRepository messages,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task<Guid?> MarkAsync(MarkReadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = await conversations.GetByIdAsync(request.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(request.ConversationId);

        if (!conversation.IsParticipant(request.ReaderUserId))
        {
            throw new ChatAccessDeniedException(request.ConversationId, request.ReaderUserId);
        }

        var now = clock.GetUtcNow();
        var anchor = await messages.MarkReadUpToAsync(
            conversation.Id, request.UpToMessageId, now, cancellationToken).ConfigureAwait(false);
        if (anchor is null)
        {
            return null;
        }

        await dispatcher.PublishAsync(
            new ChatMessageReadEvent(conversation.Id, anchor.Value, request.ReaderUserId, now),
            cancellationToken).ConfigureAwait(false);

        var observers = conversation.Participants.Where(p => p != request.ReaderUserId).ToList();
        await broadcaster.NotifyMessageReadAsync(
            observers, conversation.Id, anchor.Value, request.ReaderUserId, cancellationToken).ConfigureAwait(false);

        return anchor;
    }
}
