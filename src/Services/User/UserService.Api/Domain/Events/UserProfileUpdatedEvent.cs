using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace UserService.Api.Domain.Events;

public sealed record UserProfileUpdatedEvent(Guid UserId, string? AboutMe) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public string EventType => "user.profile.updated.v1";
    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
