namespace Urfu.Link.Services.Notification.Realtime;

public interface INotificationClient
{
    Task NotificationReceived(NotificationDto notification);

    Task NotificationUpserted(NotificationDto notification);

    Task NotificationRead(Guid notificationId);

    Task NotificationStateChanged(NotificationStateChangedDto change);

    Task NotificationRemoved(Guid notificationId);

    Task BadgeUpdated(BadgeSnapshotDto snapshot);

    Task NotificationsBatchRead(IReadOnlyList<Guid> ids);

    Task NotificationBackfillRequired(string reason);
}

public sealed record NotificationStateChangedDto(
    Guid NotificationId,
    DateTimeOffset? ReadAtUtc,
    DateTimeOffset? SeenAtUtc,
    DateTimeOffset? SavedAtUtc,
    DateTimeOffset? DoneAtUtc,
    DateTimeOffset? ArchivedAtUtc);
