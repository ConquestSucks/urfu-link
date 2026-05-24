using Urfu.Link.Services.Presence.Application.Aggregation;
using Urfu.Link.Services.Presence.Application.Privacy;
using Urfu.Link.Services.Presence.Domain.Aggregates;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Realtime;

namespace Urfu.Link.Services.Presence.Application.Sessions;

public sealed class DisconnectPresenceSessionService(
    IPresenceSessionStore sessions,
    ILastSeenRepository lastSeen,
    IPrivacyProjectionStore privacy,
    PresenceAggregator aggregator,
    PresenceBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task DisconnectAsync(Guid userId, string deviceId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        var platformBefore = await ResolvePlatformAsync(userId, deviceId, ct).ConfigureAwait(false);
        var (removed, wasLast) = await sessions.RemoveSessionAsync(userId, deviceId, ct).ConfigureAwait(false);
        if (!removed)
        {
            await BroadcastPresenceAsync(userId, ct).ConfigureAwait(false);
            return;
        }

        if (wasLast)
        {
            await PersistLastSeenAsync(userId, platformBefore, ct).ConfigureAwait(false);
        }

        await BroadcastPresenceAsync(userId, ct).ConfigureAwait(false);
    }

    private async Task<Platform> ResolvePlatformAsync(Guid userId, string deviceId, CancellationToken ct)
    {
        var current = await sessions.GetSessionsAsync(userId, ct).ConfigureAwait(false);
        return current.FirstOrDefault(session => session.DeviceId == deviceId)?.Platform ?? Platform.Web;
    }

    private async Task PersistLastSeenAsync(Guid userId, Platform platform, CancellationToken ct)
    {
        var existing = await lastSeen.GetAsync(userId, ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var entity = existing ?? LastSeen.Create(userId, platform, now);
        entity.Update(platform, now);
        lastSeen.Upsert(entity);
        await lastSeen.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task BroadcastPresenceAsync(Guid userId, CancellationToken ct)
    {
        var userSessions = await sessions.GetSessionsAsync(userId, ct).ConfigureAwait(false);
        var ls = await lastSeen.GetAsync(userId, ct).ConfigureAwait(false);
        var aggregated = aggregator.Aggregate(userId, userSessions, ls?.LastSeenAt);
        var settings = await privacy.GetAsync(userId, ct).ConfigureAwait(false);
        var publicView = PrivacyFilter.Apply(aggregated, settings);
        await broadcaster.BroadcastPresenceAsync(userId, publicView, ct).ConfigureAwait(false);
    }
}
