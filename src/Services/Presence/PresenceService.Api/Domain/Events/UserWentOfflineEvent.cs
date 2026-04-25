using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Presence.Domain.Events;

public sealed record UserWentOfflineEvent(
    Guid UserId,
    DateTimeOffset LastSeenAt,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "presence.user.offline.v1";
}
