using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Domain.Aggregates;

/// <summary>
/// Tracks that a user receives realtime updates and notifications for a thread rooted at
/// <see cref="RootMessageId"/>. <see cref="LastActivityAtUtc"/> is the denormalized sort key
/// for the user's "active threads" list, refreshed on every reply in the thread.
/// </summary>
public sealed class ThreadSubscription
{
    private ThreadSubscription(
        Guid rootMessageId,
        Guid userId,
        ThreadSubscriptionReason reason,
        DateTimeOffset subscribedAtUtc,
        DateTimeOffset lastActivityAtUtc)
    {
        RootMessageId = rootMessageId;
        UserId = userId;
        Reason = reason;
        SubscribedAtUtc = subscribedAtUtc;
        LastActivityAtUtc = lastActivityAtUtc;
    }

    public Guid RootMessageId { get; }

    public Guid UserId { get; }

    public ThreadSubscriptionReason Reason { get; private set; }

    public DateTimeOffset SubscribedAtUtc { get; }

    public DateTimeOffset LastActivityAtUtc { get; private set; }

    public static ThreadSubscription Subscribe(
        Guid rootMessageId,
        Guid userId,
        ThreadSubscriptionReason reason,
        DateTimeOffset subscribedAtUtc,
        DateTimeOffset? lastActivityAtUtc = null)
    {
        if (rootMessageId == Guid.Empty)
        {
            throw new ArgumentException("rootMessageId must be a non-empty GUID.", nameof(rootMessageId));
        }
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId must be a non-empty GUID.", nameof(userId));
        }

        return new ThreadSubscription(
            rootMessageId,
            userId,
            reason,
            subscribedAtUtc,
            lastActivityAtUtc ?? subscribedAtUtc);
    }

    public static ThreadSubscription Hydrate(
        Guid rootMessageId,
        Guid userId,
        ThreadSubscriptionReason reason,
        DateTimeOffset subscribedAtUtc,
        DateTimeOffset lastActivityAtUtc)
        => new(rootMessageId, userId, reason, subscribedAtUtc, lastActivityAtUtc);

    /// <summary>
    /// Raises <see cref="Reason"/> to <paramref name="newReason"/> if it represents stronger
    /// ownership; never downgrades. Returns <c>true</c> when the reason actually changed.
    /// </summary>
    public bool EscalateReason(ThreadSubscriptionReason newReason)
    {
        if (newReason <= Reason)
        {
            return false;
        }

        Reason = newReason;
        return true;
    }

    /// <summary>
    /// Refreshes <see cref="LastActivityAtUtc"/> to <paramref name="atUtc"/> when newer.
    /// Older or equal timestamps are ignored to avoid sort-key regressions under out-of-order writes.
    /// </summary>
    public void TouchActivity(DateTimeOffset atUtc)
    {
        if (atUtc > LastActivityAtUtc)
        {
            LastActivityAtUtc = atUtc;
        }
    }
}
