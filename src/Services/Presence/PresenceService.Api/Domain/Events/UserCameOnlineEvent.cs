using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Presence.Domain.Enums;

namespace Urfu.Link.Services.Presence.Domain.Events;

public sealed record UserCameOnlineEvent(
    Guid UserId,
    IReadOnlyList<Platform> Platforms,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "presence.user.online.v1";
}
