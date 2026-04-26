using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Threads;

public sealed record LeaveThreadRequest(Guid CallerUserId, Guid RootMessageId);

/// <summary>
/// Removes the caller's subscription from a thread. Reply history is untouched — the user
/// just stops receiving thread updates. Calling on a non-existent subscription is a no-op.
/// </summary>
public sealed class LeaveThreadService(
    IThreadSubscriptionRepository subscriptions,
    ChatEventDispatcher dispatcher,
    TimeProvider clock)
{
    public async Task LeaveAsync(LeaveThreadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var removed = await subscriptions.RemoveAsync(request.RootMessageId, request.CallerUserId, cancellationToken)
            .ConfigureAwait(false);
        if (!removed)
        {
            return;
        }

        var now = clock.GetUtcNow();
        await dispatcher.PublishAsync(
            new ChatThreadSubscriptionChangedEvent(
                request.RootMessageId, request.CallerUserId, Subscribed: false, ThreadSubscriptionReason.Manual, now),
            cancellationToken).ConfigureAwait(false);
    }
}
