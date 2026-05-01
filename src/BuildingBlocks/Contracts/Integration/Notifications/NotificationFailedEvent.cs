namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Notifications;

public sealed record NotificationFailedEvent(
    Guid NotificationId,
    Guid RecipientUserId,
    int Category,
    int Channel,
    string Reason) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "notification.failed.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
