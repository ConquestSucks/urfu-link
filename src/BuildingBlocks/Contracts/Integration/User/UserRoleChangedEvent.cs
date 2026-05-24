namespace Urfu.Link.BuildingBlocks.Contracts.Integration.User;

public sealed record UserRoleChangedEvent(
    Guid UserId,
    string OldRole,
    string NewRole) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "user.role_changed.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
