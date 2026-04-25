using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Application.Aggregation;

public sealed class PresenceAggregator
{
    public AggregatedPresence Aggregate(
        Guid userId,
        IReadOnlyList<PresenceSession> sessions,
        DateTimeOffset? lastSeenAt)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        if (sessions.Count == 0)
        {
            return AggregatedPresence.Offline(userId, lastSeenAt);
        }

        var status = PickAggregateStatus(sessions);
        var platforms = sessions
            .Select(s => s.Platform)
            .Distinct()
            .OrderBy(p => p)
            .ToArray();

        return new AggregatedPresence(userId, status, platforms, lastSeenAt);
    }

    private static PresenceStatus PickAggregateStatus(IReadOnlyList<PresenceSession> sessions)
    {
        if (sessions.Any(s => s.Status == PresenceStatus.Online)) return PresenceStatus.Online;
        if (sessions.Any(s => s.Status == PresenceStatus.Away)) return PresenceStatus.Away;
        if (sessions.Any(s => s.Status == PresenceStatus.DoNotDisturb)) return PresenceStatus.DoNotDisturb;
        if (sessions.Any(s => s.Status == PresenceStatus.Invisible)) return PresenceStatus.Invisible;
        return PresenceStatus.Offline;
    }
}
