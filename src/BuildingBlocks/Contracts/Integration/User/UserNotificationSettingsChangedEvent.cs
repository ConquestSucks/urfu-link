namespace Urfu.Link.BuildingBlocks.Contracts.Integration.User;

public sealed record UserNotificationSettingsChangedEvent(
    Guid UserId,
    NotificationPreferencesPayload Preferences) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "user.notification_settings_changed.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
