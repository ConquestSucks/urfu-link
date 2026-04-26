using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Application.Direct;

/// <summary>
/// Internal command-style event used by the gRPC SendDirectNotification entrypoint to
/// route an ad-hoc notification through the standard pipeline. Not published to Kafka.
/// </summary>
public sealed record DirectNotificationCommand(
    Guid RecipientUserId,
    NotificationCategory Category,
    NotificationSeverity Severity,
    string Title,
    string Body,
    string? DeepLink,
    IReadOnlyDictionary<string, string> Data,
    Guid SourceId,
    string SourceEventType) : IIntegrationEvent
{
    public Guid EventId { get; } = SourceId == Guid.Empty ? Guid.NewGuid() : SourceId;

    public string EventType => SourceEventType;

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
