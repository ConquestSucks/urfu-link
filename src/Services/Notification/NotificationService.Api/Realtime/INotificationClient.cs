namespace Urfu.Link.Services.Notification.Realtime;

public interface INotificationClient
{
    Task NotificationReceived(NotificationDto notification);

    Task NotificationRead(Guid notificationId);

    Task BadgeUpdated(BadgeSnapshotDto snapshot);

    Task NotificationsBatchRead(IReadOnlyList<Guid> ids);
}
