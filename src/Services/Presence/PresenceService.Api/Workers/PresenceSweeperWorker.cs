using Microsoft.Extensions.Options;
using Urfu.Link.Services.Presence.Domain.Aggregates;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Infrastructure;

namespace Urfu.Link.Services.Presence.Workers;

/// <summary>
/// Periodically cleans up sessions whose heartbeat exceeded
/// <see cref="PresenceOptions.SessionTtl"/>. When a swept session was the
/// user's last one, persists last_seen and dispatches
/// <c>UserWentOfflineEvent</c> via the LastSeen aggregate's domain events.
/// Idempotent against races with <c>PresenceHub.OnDisconnectedAsync</c>:
/// <c>RemoveSessionAsync</c> returns <c>removed=false</c> on the second call,
/// so only the first one publishes the offline event.
/// </summary>
public sealed class PresenceSweeperWorker(
    IServiceScopeFactory scopeFactory,
    IPresenceSessionStore sessions,
    IOptions<PresenceOptions> options,
    TimeProvider clock,
    ILogger<PresenceSweeperWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "PresenceSweeper iteration failed");
            }

            try
            {
                await Task.Delay(options.Value.SweeperInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - options.Value.SessionTtl;
        var expired = await sessions
            .GetExpiredSessionsAsync(cutoff, limit: 200, cancellationToken)
            .ConfigureAwait(false);
        if (expired.Count == 0) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var lastSeenRepo = scope.ServiceProvider.GetRequiredService<ILastSeenRepository>();

        foreach (var (userId, deviceId) in expired)
        {
            var platformBefore = await ResolvePlatformAsync(userId, deviceId, cancellationToken).ConfigureAwait(false);
            var (removed, wasLast) = await sessions
                .RemoveSessionAsync(userId, deviceId, cancellationToken)
                .ConfigureAwait(false);

            if (removed && wasLast)
            {
                var existing = await lastSeenRepo.GetAsync(userId, cancellationToken).ConfigureAwait(false);
                var now = clock.GetUtcNow();
                var entity = existing ?? LastSeen.Create(userId, platformBefore, now);
                entity.Update(platformBefore, now);
                lastSeenRepo.Upsert(entity);
            }
        }

        await lastSeenRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Platform> ResolvePlatformAsync(Guid userId, string deviceId, CancellationToken ct)
    {
        var current = await sessions.GetSessionsAsync(userId, ct).ConfigureAwait(false);
        var match = current.FirstOrDefault(s => s.DeviceId == deviceId);
        return match?.Platform ?? Platform.Web;
    }
}
