using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Notification.Domain.Events;

public sealed record NotificationReadEvent(
    Guid NotificationId,
    Guid RecipientUserId,
    DateTimeOffset ReadAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "notification.read.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
