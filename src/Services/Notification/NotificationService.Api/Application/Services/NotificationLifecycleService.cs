using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Realtime;

namespace Urfu.Link.Services.Notification.Application.Services;

public sealed class NotificationLifecycleService(
    INotificationRepository repository,
    IBadgeStore badgeStore,
    INotificationBroadcaster broadcaster)
{
    public async Task<int> ArchiveBySourceActionAsync(
        string sourceActionId,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken)
    {
        var archived = await repository.ArchiveBySourceActionAsync(sourceActionId, archivedAtUtc, cancellationToken)
            .ConfigureAwait(false);
        if (archived.Count == 0)
        {
            return 0;
        }

        foreach (var group in archived.GroupBy(n => n.RecipientUserId))
        {
            foreach (var notification in group)
            {
                await broadcaster.NotifyRemovedAsync(group.Key, notification.Id, cancellationToken).ConfigureAwait(false);
            }

            var counts = await repository.CountBadgeAsync(group.Key, cancellationToken).ConfigureAwait(false);
            var snapshot = new BadgeSnapshot(counts.TotalUnread, counts.PerCategory);
            await badgeStore.SetSnapshotAsync(group.Key, snapshot, cancellationToken).ConfigureAwait(false);
            await broadcaster.NotifyBadgeUpdatedAsync(
                group.Key,
                new BadgeSnapshotDto(
                    snapshot.Total,
                    snapshot.PerCategory.ToDictionary(kv => (int)kv.Key, kv => kv.Value),
                    counts.TotalUnseen,
                    counts.UrgentUnread,
                    counts.PerType),
                cancellationToken).ConfigureAwait(false);
        }

        return archived.Count;
    }
}
