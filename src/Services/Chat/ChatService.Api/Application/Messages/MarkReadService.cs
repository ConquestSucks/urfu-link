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
    /// <summary>
    /// Marks every message up to and including <c>UpToMessageId</c> as read by the caller,
    /// populates each message's <c>ReadBy[]</c>, and emits both the legacy
    /// <c>MessageReadUpdate</c> anchor and the per-message <c>MessageReadByUpdate</c>
    /// broadcasts. Returns the new anchor, or <see langword="null"/> if the caller had
    /// already read the range.
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
        var newlyRead = await messages.MarkReadUpToAsync(
            conversation.Id, request.ReaderUserId, request.UpToMessageId, now, cancellationToken).ConfigureAwait(false);
        if (newlyRead.Count == 0)
        {
            return null;
        }

        var anchor = newlyRead[^1];
        var observers = conversation.Participants.Where(p => p != request.ReaderUserId).ToList();

        await dispatcher.PublishAsync(
            new ChatMessageReadEvent(conversation.Id, anchor, request.ReaderUserId, now),
            cancellationToken).ConfigureAwait(false);

        foreach (var messageId in newlyRead)
        {
            await broadcaster.NotifyMessageReadByAsync(
                observers, conversation.Id, messageId, request.ReaderUserId, now, cancellationToken).ConfigureAwait(false);
        }

        // Legacy anchor-only broadcast, kept for clients that have not adopted
        // MessageReadByUpdate yet.
        await broadcaster.NotifyMessageReadAsync(
            observers, conversation.Id, anchor, request.ReaderUserId, cancellationToken).ConfigureAwait(false);

        return anchor;
    }
}
