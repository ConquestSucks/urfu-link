using Urfu.Link.Services.Notification.Realtime;

namespace NotificationService.IntegrationTests.Infrastructure;

/// <summary>
/// Bypasses SignalR for tests: integration tests don't run a real WebSocket client,
/// they just assert the routing pipeline and the resulting database state. The real
/// broadcaster reaches into <see cref="Microsoft.AspNetCore.SignalR.IHubContext{T,T}"/>
/// which talks to the Redis backplane configured by Program.cs at host startup —
/// which doesn't exist in the test container topology.
/// </summary>
public sealed class NoopNotificationBroadcaster : INotificationBroadcaster
{
    public Task NotifyReceivedAsync(NotificationDto notification, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyUpsertedAsync(NotificationDto notification, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyReadAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyStateChangedAsync(Guid recipientUserId, NotificationStateChangedDto change, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyRemovedAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyBatchReadAsync(Guid recipientUserId, IReadOnlyList<Guid> notificationIds, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyBadgeUpdatedAsync(Guid recipientUserId, BadgeSnapshotDto snapshot, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyBackfillRequiredAsync(Guid recipientUserId, string reason, CancellationToken cancellationToken) => Task.CompletedTask;
}
