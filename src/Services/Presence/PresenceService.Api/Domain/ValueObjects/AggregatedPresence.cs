using Urfu.Link.Services.Presence.Domain.Enums;

namespace Urfu.Link.Services.Presence.Domain.ValueObjects;

public sealed record AggregatedPresence(
    Guid UserId,
    PresenceStatus Status,
    IReadOnlyList<Platform> Platforms,
    DateTimeOffset? LastSeenAt)
{
    public static AggregatedPresence Offline(Guid userId, DateTimeOffset? lastSeenAt = null)
        => new(userId, PresenceStatus.Offline, [], lastSeenAt);
}
