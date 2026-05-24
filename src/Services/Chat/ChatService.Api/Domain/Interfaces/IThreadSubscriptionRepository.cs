using Urfu.Link.Services.Chat.Domain.Aggregates;

namespace Urfu.Link.Services.Chat.Domain.Interfaces;

/// <summary>
/// Outcome of an upsert: was the subscription created, was the existing one's reason escalated,
/// or was it a no-op (already at or above the requested reason).
/// </summary>
public readonly record struct ThreadSubscriptionUpsertResult(bool WasCreated, bool ReasonEscalated)
{
    public bool RequiresEvent => WasCreated || ReasonEscalated;
}

public interface IThreadSubscriptionRepository
{
    /// <summary>
    /// Creates the subscription if missing, or escalates <see cref="ThreadSubscription.Reason"/>
    /// when the new reason is higher priority. Always advances <c>LastActivityAtUtc</c> via the
    /// aggregate's <see cref="ThreadSubscription.TouchActivity"/> rules.
    /// </summary>
    Task<ThreadSubscriptionUpsertResult> UpsertAsync(
        ThreadSubscription subscription,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the explicit subscription. Returns <see langword="false"/> if no subscription
    /// existed for the (root, user) pair.
    /// </summary>
    Task<bool> RemoveAsync(Guid rootMessageId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(Guid rootMessageId, CancellationToken cancellationToken);

    Task<bool> IsSubscribedAsync(Guid rootMessageId, Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Bulk-refreshes <c>LastActivityAtUtc</c> on every subscription for the given root, so the
    /// thread floats to the top of every subscriber's "active threads" list. The mongo update is
    /// monotonic — older timestamps cannot overwrite newer ones.
    /// </summary>
    Task<long> TouchActivityForRootAsync(
        Guid rootMessageId,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cursor-paginated list of the user's subscriptions, sorted by LastActivityAtUtc desc then
    /// RootMessageId desc as a deterministic tie-breaker.
    /// </summary>
    Task<IReadOnlyList<ThreadSubscription>> ListUserActiveAsync(
        Guid userId,
        ThreadActivityCursor? cursor,
        int limit,
        CancellationToken cancellationToken);
}
