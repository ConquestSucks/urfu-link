using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace UserService.Api.Domain.Events;

public sealed record UserAvatarChangedEvent(Guid UserId, string? AvatarUrl) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public string EventType => "user.avatar.changed.v1";
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
