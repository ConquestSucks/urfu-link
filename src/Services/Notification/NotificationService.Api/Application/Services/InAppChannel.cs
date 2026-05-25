using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Application.Services;

/// <summary>
/// Pushes the in-app notification to all of the recipient's open SignalR connections
/// and refreshes their badge snapshot. Email and push channels live in their own
/// background dispatchers (Wave 10/11) so this channel runs synchronously inside the
/// router transaction.
/// </summary>
public sealed class InAppChannel(INotificationBroadcaster broadcaster, BadgeService badgeService)
{
    public async Task DeliverAsync(NotificationAggregate notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var dto = NotificationDtoMapper.Map(notification);
        await broadcaster.NotifyReceivedAsync(dto, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyUpsertedAsync(dto, cancellationToken).ConfigureAwait(false);

        var snapshot = await badgeService.GetSnapshotAsync(notification.RecipientUserId, cancellationToken)
            .ConfigureAwait(false);
        await broadcaster.NotifyBadgeUpdatedAsync(notification.RecipientUserId, snapshot, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertAsync(NotificationAggregate notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        await broadcaster.NotifyUpsertedAsync(NotificationDtoMapper.Map(notification), cancellationToken)
            .ConfigureAwait(false);
        var snapshot = await badgeService.GetSnapshotAsync(notification.RecipientUserId, cancellationToken)
            .ConfigureAwait(false);
        await broadcaster.NotifyBadgeUpdatedAsync(notification.RecipientUserId, snapshot, cancellationToken)
            .ConfigureAwait(false);
    }
}
