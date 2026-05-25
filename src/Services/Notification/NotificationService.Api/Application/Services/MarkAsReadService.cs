using Urfu.Link.BuildingBlocks.Contracts.Integration.Notifications;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Outbox;
using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Application.Services;

/// <summary>
/// Marks notifications as read on behalf of a user, updates badge counters and
/// notifies connected SignalR clients so other devices stay in sync.
/// </summary>
public sealed class MarkAsReadService(
    INotificationRepository repository,
    IBadgeStore badgeStore,
    INotificationBroadcaster broadcaster,
    IOutboxEnqueue outboxEnqueue,
    TimeProvider timeProvider)
{
    public async Task MarkSingleAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await repository.GetByIdAsync(notificationId, userId, cancellationToken).ConfigureAwait(false);
        if (notification is null)
        {
            return;
        }

        var readAt = timeProvider.GetUtcNow();
        var changed = notification.MarkRead(readAt);
        if (!changed)
        {
            return;
        }

        outboxEnqueue.Enqueue(new NotificationReadEvent(notification.Id, userId, readAt));

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await badgeStore.DecrementAsync(userId, notification.Category, cancellationToken).ConfigureAwait(false);

        await broadcaster.NotifyReadAsync(userId, notification.Id, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyStateChangedAsync(userId, MapChange(notification), cancellationToken).ConfigureAwait(false);
        await BroadcastBadgeAsync(userId, cancellationToken).ConfigureAwait(false);
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

    public async Task<bool> ApplyActionAsync(
        Guid userId,
        Guid notificationId,
        string action,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var notification = await repository.GetByIdAsync(notificationId, userId, cancellationToken).ConfigureAwait(false);
        if (notification is null)
        {
            return false;
        }

        var changed = Apply(notification, action, timeProvider.GetUtcNow(), out var badgeDelta);
        if (!changed)
        {
            return false;
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await ApplyBadgeDeltaAsync(userId, notification.Category, badgeDelta, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyStateChangedAsync(userId, MapChange(notification), cancellationToken).ConfigureAwait(false);
        await BroadcastBadgeAsync(userId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> ApplyBulkAsync(
        Guid userId,
        IReadOnlyList<Guid>? ids,
        NotificationListFilter filter,
        string action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var notifications = await repository.ListForBulkAsync(userId, filter, ids, 1000, cancellationToken)
            .ConfigureAwait(false);
        if (notifications.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var changed = new List<NotificationAggregate>(notifications.Count);
        foreach (var notification in notifications)
        {
            if (Apply(notification, action, now, out _))
            {
                changed.Add(notification);
            }
        }

        if (changed.Count == 0)
        {
            return 0;
        }

        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var unreadPerCategory = await repository.CountUnreadPerCategoryAsync(userId, cancellationToken).ConfigureAwait(false);
        var total = unreadPerCategory.Values.Sum();
        await badgeStore.SetSnapshotAsync(userId, new BadgeSnapshot(total, unreadPerCategory), cancellationToken).ConfigureAwait(false);

        foreach (var notification in changed)
        {
            await broadcaster.NotifyStateChangedAsync(userId, MapChange(notification), cancellationToken).ConfigureAwait(false);
        }

        await BroadcastBadgeAsync(userId, cancellationToken).ConfigureAwait(false);
        return changed.Count;
    }

    private static bool Apply(
        NotificationAggregate notification,
        string action,
        DateTimeOffset now,
        out int badgeDelta)
    {
        badgeDelta = 0;
        var wasUnread = notification.ReadAtUtc is null && notification.DoneAtUtc is null && notification.ArchivedAtUtc is null;
        var changed = action.Trim().ToUpperInvariant() switch
        {
            "READ" => notification.MarkRead(now),
            "UNREAD" => notification.MarkUnread(),
            "SEEN" => notification.MarkSeen(now),
            "SAVE" => notification.Save(now),
            "UNSAVE" => notification.Unsave(),
            "DONE" => notification.MarkDone(now),
            "RESTORE" => notification.Restore(),
            "ARCHIVE" => notification.Archive(now),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown notification action."),
        };

        var isUnread = notification.ReadAtUtc is null && notification.DoneAtUtc is null && notification.ArchivedAtUtc is null;
        badgeDelta = (wasUnread, isUnread) switch
        {
            (true, false) => -1,
            (false, true) => 1,
            _ => 0,
        };

        return changed;
    }

    private async Task ApplyBadgeDeltaAsync(
        Guid userId,
        NotificationCategory category,
        int badgeDelta,
        CancellationToken cancellationToken)
    {
        if (badgeDelta < 0)
        {
            await badgeStore.DecrementAsync(userId, category, cancellationToken).ConfigureAwait(false);
        }
        else if (badgeDelta > 0)
        {
            await badgeStore.IncrementAsync(userId, category, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task BroadcastBadgeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await badgeStore.GetSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyBadgeUpdatedAsync(userId, MapSnapshot(snapshot), cancellationToken).ConfigureAwait(false);
    }

    private static NotificationStateChangedDto MapChange(NotificationAggregate notification)
        => new(
            notification.Id,
            notification.ReadAtUtc,
            notification.SeenAtUtc,
            notification.SavedAtUtc,
            notification.DoneAtUtc,
            notification.ArchivedAtUtc);

    private static BadgeSnapshotDto MapSnapshot(BadgeSnapshot snapshot)
    {
        var perCategory = snapshot.PerCategory.ToDictionary(kv => (int)kv.Key, kv => kv.Value);
        return new BadgeSnapshotDto(snapshot.Total, perCategory);
    }
}
