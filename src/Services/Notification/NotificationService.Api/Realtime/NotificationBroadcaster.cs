using Microsoft.AspNetCore.SignalR;

namespace Urfu.Link.Services.Notification.Realtime;

public sealed class NotificationBroadcaster(IHubContext<NotificationHub, INotificationClient> hub)
    : INotificationBroadcaster
{
    private readonly IHubContext<NotificationHub, INotificationClient> _hub = hub;

    public Task NotifyReceivedAsync(NotificationDto notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _ = cancellationToken;
        return _hub.Clients.Group(NotificationHub.GroupForUser(notification.RecipientUserId))
            .NotificationReceived(notification);
    }

    public Task NotifyUpsertedAsync(NotificationDto notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        _ = cancellationToken;
        return _hub.Clients.Group(NotificationHub.GroupForUser(notification.RecipientUserId))
            .NotificationUpserted(notification);
    }

    public Task NotifyReadAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return _hub.Clients.Group(NotificationHub.GroupForUser(recipientUserId))
            .NotificationRead(notificationId);
    }

    public Task NotifyStateChangedAsync(Guid recipientUserId, NotificationStateChangedDto change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        _ = cancellationToken;
        return _hub.Clients.Group(NotificationHub.GroupForUser(recipientUserId))
            .NotificationStateChanged(change);
    }

    public Task NotifyRemovedAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return _hub.Clients.Group(NotificationHub.GroupForUser(recipientUserId))
            .NotificationRemoved(notificationId);
    }

    public Task NotifyBadgeUpdatedAsync(Guid recipientUserId, BadgeSnapshotDto snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _ = cancellationToken;
        return _hub.Clients.Group(NotificationHub.GroupForUser(recipientUserId))
            .BadgeUpdated(snapshot);
    }

    public Task NotifyBatchReadAsync(Guid recipientUserId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificationIds);
        _ = cancellationToken;
        return _hub.Clients.Group(NotificationHub.GroupForUser(recipientUserId))
            .NotificationsBatchRead(notificationIds);
    }

    public Task NotifyBackfillRequiredAsync(Guid recipientUserId, string reason, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return _hub.Clients.Group(NotificationHub.GroupForUser(recipientUserId))
            .NotificationBackfillRequired(reason);
    }
}
