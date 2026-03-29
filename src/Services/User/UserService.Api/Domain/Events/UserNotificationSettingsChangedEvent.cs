using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace UserService.Api.Domain.Events;

public sealed record UserNotificationSettingsChangedEvent(
    Guid UserId,
    bool NewMessages,
    bool NotificationSound,
    bool DisciplineChatMessages,
    bool Mentions) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public string EventType => "user.notifications.changed.v1";
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
