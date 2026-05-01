namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Notifications;

public sealed record NotificationReadEvent(
    Guid NotificationId,
    Guid RecipientUserId,
    DateTimeOffset ReadAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "notification.read.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
