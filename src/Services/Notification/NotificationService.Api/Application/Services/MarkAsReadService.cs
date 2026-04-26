using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Realtime;

namespace Urfu.Link.Services.Notification.Application.Services;

/// <summary>
/// Marks notifications as read on behalf of a user, updates badge counters and
/// notifies connected SignalR clients so other devices stay in sync.
/// </summary>
public sealed class MarkAsReadService(
    INotificationRepository repository,
    IBadgeStore badgeStore,
    INotificationBroadcaster broadcaster,
    TimeProvider timeProvider)
{
    public async Task MarkSingleAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await repository.GetByIdAsync(notificationId, userId, cancellationToken).ConfigureAwait(false);
        if (notification is null)
        {
            return;
        }

        var changed = notification.MarkRead(timeProvider.GetUtcNow());
        if (!changed)
        {
            return;
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await badgeStore.DecrementAsync(userId, notification.Category, cancellationToken).ConfigureAwait(false);

        await broadcaster.NotifyReadAsync(userId, notification.Id, cancellationToken).ConfigureAwait(false);
        var snapshot = await badgeStore.GetSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyBadgeUpdatedAsync(userId, MapSnapshot(snapshot), cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> MarkAllAsync(Guid userId, NotificationCategory? category, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var updated = await repository.MarkAllAsReadAsync(userId, category, now, cancellationToken).ConfigureAwait(false);
        if (updated == 0)
        {
            return 0;
        }

        // Rebuild badge from DB to keep Redis in sync after a bulk mutation.
        var unreadPerCategory = await repository.CountUnreadPerCategoryAsync(userId, cancellationToken).ConfigureAwait(false);
        var total = unreadPerCategory.Values.Sum();
        await badgeStore.SetSnapshotAsync(userId, new BadgeSnapshot(total, unreadPerCategory), cancellationToken).ConfigureAwait(false);

        var snapshot = await badgeStore.GetSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyBadgeUpdatedAsync(userId, MapSnapshot(snapshot), cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private static BadgeSnapshotDto MapSnapshot(BadgeSnapshot snapshot)
    {
        var perCategory = snapshot.PerCategory.ToDictionary(kv => (int)kv.Key, kv => kv.Value);
        return new BadgeSnapshotDto(snapshot.Total, perCategory);
    }
}
