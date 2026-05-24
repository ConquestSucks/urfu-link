using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Application.Contracts.Responses;

public sealed record PresenceInfoResponse(
    Guid UserId,
    PresenceStatus Status,
    Platform[] Platforms,
    DateTimeOffset? LastSeenAt)
{
    public static PresenceInfoResponse From(AggregatedPresence agg)
    {
        ArgumentNullException.ThrowIfNull(agg);
        return new PresenceInfoResponse(agg.UserId, agg.Status, agg.Platforms.ToArray(), agg.LastSeenAt);
    }
}
