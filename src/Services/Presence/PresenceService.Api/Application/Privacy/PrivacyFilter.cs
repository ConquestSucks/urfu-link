using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Application.Privacy;

public static class PrivacyFilter
{
    public static AggregatedPresence Apply(AggregatedPresence original, PrivacySettings settings)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(settings);

        var lastSeen = settings.ShowLastVisitTime ? null : original.LastSeenAt;

        return new AggregatedPresence(original.UserId, original.Status, original.Platforms, lastSeen);
    }
}
