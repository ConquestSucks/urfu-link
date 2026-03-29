using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace UserService.Api.Domain.Events;

public sealed record UserPrivacySettingsChangedEvent(
    Guid UserId,
    bool ShowOnlineStatus,
    bool ShowLastVisitTime) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public string EventType => "user.privacy.changed.v1";
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
