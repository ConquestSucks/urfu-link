using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Events;

public sealed record NotificationDeliveredEvent(
    Guid NotificationId,
    Guid RecipientUserId,
    NotificationCategory Category,
    DeliveryChannel Channel,
    DateTimeOffset DeliveredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "notification.delivered.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
