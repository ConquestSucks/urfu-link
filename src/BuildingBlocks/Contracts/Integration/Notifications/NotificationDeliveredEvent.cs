namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Notifications;

public sealed record NotificationDeliveredEvent(
    Guid NotificationId,
    Guid RecipientUserId,
    int Category,
    int Channel,
    DateTimeOffset DeliveredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "notification.delivered.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
