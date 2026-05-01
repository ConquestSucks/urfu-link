using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Events;

public sealed record NotificationCreatedEvent(
    Guid NotificationId,
    Guid RecipientUserId,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string? GroupKey,
    Guid SourceEventId,
    string SourceEventType) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "notification.created.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
