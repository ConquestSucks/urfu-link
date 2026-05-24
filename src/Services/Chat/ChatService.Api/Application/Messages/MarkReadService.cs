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
    /// <summary>
    /// Marks every message up to and including <c>UpToMessageId</c> as Read, populates
    /// each message's <c>ReadBy[]</c> with the reader, and emits both the legacy
    /// <c>MessageReadUpdate</c> (anchor) and the per-message <c>MessageReadByUpdate</c>
    /// broadcasts. Returns the new anchor (last transitioned id) or <see langword="null"/>
    /// if nothing transitioned — preserving the wire contract for existing hub clients.
    /// </summary>
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
        var transitioned = await messages.MarkReadUpToAsync(
            conversation.Id, request.UpToMessageId, now, cancellationToken).ConfigureAwait(false);
        if (transitioned.Count == 0)
        {
            return null;
        }

        var anchor = transitioned[^1];
        var observers = conversation.Participants.Where(p => p != request.ReaderUserId).ToList();

        await dispatcher.PublishAsync(
            new ChatMessageReadEvent(conversation.Id, anchor, request.ReaderUserId, now),
            cancellationToken).ConfigureAwait(false);

        // Group-aware read receipts: append a receipt for every transitioned message and emit a
        // dedicated broadcast per id. Direct conversations land here too (anchor is usually the
        // single transitioned message), but this keeps the data shape ready for #214 group chats.
        foreach (var messageId in transitioned)
        {
            await messages.AddReadByAsync(
                messageId, new ReadReceipt(request.ReaderUserId, now), cancellationToken).ConfigureAwait(false);
            await broadcaster.NotifyMessageReadByAsync(
                observers, conversation.Id, messageId, request.ReaderUserId, now, cancellationToken).ConfigureAwait(false);
        }

        // Legacy anchor-only broadcast — kept for backward compat with clients that haven't
        // adopted MessageReadByUpdate yet.
        await broadcaster.NotifyMessageReadAsync(
            observers, conversation.Id, anchor, request.ReaderUserId, cancellationToken).ConfigureAwait(false);

        return anchor;
    }
}
