namespace Urfu.Link.Services.Notification.Realtime;

public interface INotificationBroadcaster
{
    Task NotifyReceivedAsync(NotificationDto notification, CancellationToken cancellationToken);

    Task NotifyUpsertedAsync(NotificationDto notification, CancellationToken cancellationToken);

    Task NotifyReadAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken);

    Task NotifyStateChangedAsync(Guid recipientUserId, NotificationStateChangedDto change, CancellationToken cancellationToken);

    Task NotifyRemovedAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken);

    Task NotifyBadgeUpdatedAsync(Guid recipientUserId, BadgeSnapshotDto snapshot, CancellationToken cancellationToken);

    Task NotifyBatchReadAsync(Guid recipientUserId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken);

    Task NotifyBackfillRequiredAsync(Guid recipientUserId, string reason, CancellationToken cancellationToken);
}
