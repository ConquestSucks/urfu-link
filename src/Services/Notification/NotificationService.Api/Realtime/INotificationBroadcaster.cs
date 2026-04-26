namespace Urfu.Link.Services.Notification.Realtime;

public interface INotificationBroadcaster
{
    Task NotifyReceivedAsync(NotificationDto notification, CancellationToken cancellationToken);

    Task NotifyReadAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken);

    Task NotifyBadgeUpdatedAsync(Guid recipientUserId, BadgeSnapshotDto snapshot, CancellationToken cancellationToken);

    Task NotifyBatchReadAsync(Guid recipientUserId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken);
}
