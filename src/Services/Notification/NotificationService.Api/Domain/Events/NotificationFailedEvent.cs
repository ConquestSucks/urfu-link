using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Events;

public sealed record NotificationFailedEvent(
    Guid NotificationId,
    Guid RecipientUserId,
    NotificationCategory Category,
    DeliveryChannel Channel,
    string Reason) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "notification.failed.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
