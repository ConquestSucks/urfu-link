using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
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

        // Group-aware read receipts: also append to the anchor's ReadBy[] and emit the
        // group-flavoured broadcast. For Direct conversations this is essentially a noop on the
        // wire (the existing scalar ReadAtUtc still drives client UI), but the data is in place
        // for #214 when discipline group chats start populating ReadBy across many readers.
        await messages.AddReadByAsync(
            anchor.Value, new ReadReceipt(request.ReaderUserId, now), cancellationToken).ConfigureAwait(false);

        var observers = conversation.Participants.Where(p => p != request.ReaderUserId).ToList();
        await broadcaster.NotifyMessageReadAsync(
            observers, conversation.Id, anchor.Value, request.ReaderUserId, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyMessageReadByAsync(
            observers, conversation.Id, anchor.Value, request.ReaderUserId, now, cancellationToken).ConfigureAwait(false);

        return anchor;
    }
}
