using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Application.Privacy;

public static class PrivacyFilter
{
    public static AggregatedPresence Apply(AggregatedPresence original, PrivacySettings settings)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(settings);

        var status = settings.ShowOnlineStatus ? original.Status : PresenceStatus.Offline;
        IReadOnlyList<Platform> platforms = settings.ShowOnlineStatus ? original.Platforms : [];
        var lastSeen = settings.ShowLastVisitTime ? original.LastSeenAt : null;

        return new AggregatedPresence(original.UserId, status, platforms, lastSeen);
    }
}
