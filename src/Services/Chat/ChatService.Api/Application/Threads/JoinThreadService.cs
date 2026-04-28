using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Threads;

public sealed record JoinThreadRequest(Guid CallerUserId, Guid RootMessageId);

/// <summary>
/// Manually subscribes a user to the thread rooted at <see cref="JoinThreadRequest.RootMessageId"/>.
/// Only conversation participants may join; replies and tombstoned roots are rejected. Idempotent
/// at the repository level — joining an existing subscription is a no-op event-wise but does not
/// fail.
/// </summary>
public sealed class JoinThreadService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IThreadSubscriptionRepository subscriptions,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task JoinAsync(JoinThreadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var root = await messages.GetByIdAsync(request.RootMessageId, cancellationToken).ConfigureAwait(false)
            ?? throw ChatThreadRootNotFoundException.For(request.RootMessageId);

        if (root.IsThreadReply)
        {
            throw ChatThreadCannotReplyToReplyException.For(root.Id);
        }

        if (root.State == MessageState.Deleted)
        {
            throw ChatThreadRootNotFoundException.For(request.RootMessageId);
        }

        var conversation = await conversations.GetByIdAsync(root.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(root.ConversationId);

        if (!conversation.IsParticipant(request.CallerUserId))
        {
            throw new ChatAccessDeniedException(conversation.Id, request.CallerUserId);
        }

        var now = clock.GetUtcNow();
        // For a fresh subscriber, lastActivity defaults to the root's actual lastReplyAt so the
        // newly-active thread shows up at the correct chronological position; if the root never
        // had a reply, we fall back to "now" so the user's join still surfaces it.
        var lastActivity = root.ThreadLastReplyAtUtc ?? now;

        var subscription = ThreadSubscription.Subscribe(
            root.Id, request.CallerUserId, ThreadSubscriptionReason.Manual, now, lastActivity);
        var result = await subscriptions.UpsertAsync(subscription, cancellationToken).ConfigureAwait(false);

        if (!result.RequiresEvent)
        {
            // Already subscribed at an equal-or-higher reason — nothing to publish or broadcast.
            return;
        }

        await dispatcher.PublishAsync(
            new ChatThreadSubscriptionChangedEvent(
                root.Id, request.CallerUserId, Subscribed: true, ThreadSubscriptionReason.Manual, now),
            cancellationToken).ConfigureAwait(false);

        var subscribers = await subscriptions.GetSubscriberIdsAsync(root.Id, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyThreadParticipantJoinedAsync(
            subscribers, root.Id, request.CallerUserId, ThreadSubscriptionReason.Manual, cancellationToken)
            .ConfigureAwait(false);
    }
}
