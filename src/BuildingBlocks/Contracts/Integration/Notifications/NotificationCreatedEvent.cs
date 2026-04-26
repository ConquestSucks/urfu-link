namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Notifications;

public sealed record NotificationCreatedEvent(
    Guid NotificationId,
    Guid RecipientUserId,
    int Category,
    Guid SourceEventId,
    string SourceEventType) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "notification.created.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
