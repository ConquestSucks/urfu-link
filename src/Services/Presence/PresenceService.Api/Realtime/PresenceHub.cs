using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Presence.Application.Aggregation;
using Urfu.Link.Services.Presence.Application.Dispatchers;
using Urfu.Link.Services.Presence.Application.Privacy;
using Urfu.Link.Services.Presence.Domain.Aggregates;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Realtime;

[Authorize]
public sealed class PresenceHub(
    IPresenceSessionStore sessions,
    ITypingStore typing,
    ILastSeenRepository lastSeen,
    IPrivacyProjectionStore privacy,
    PresenceAggregator aggregator,
    PresenceBroadcaster broadcaster,
    PresenceEventDispatcher dispatcher,
    TimeProvider clock) : Hub<IPresenceClient>
{
    private const string DeviceIdItemKey = "deviceId";
    private const string UserIdItemKey = "userId";
    private const string PlatformItemKey = "platform";

    public static string GroupForUser(Guid userId) =>
        string.Create(CultureInfo.InvariantCulture, $"presence:{userId:N}");

    public override async Task OnConnectedAsync()
    {
        // Hub lifecycle hooks must finish even if the client races to close —
        // ConnectionAborted can fire mid-connect on long-polling transports.
        var ct = CancellationToken.None;
        var userId = ResolveUserId(Context.User);
        var deviceId = ResolveDeviceId(Context);
        var platform = ResolvePlatform(Context);
        var now = clock.GetUtcNow();

        Context.Items[UserIdItemKey] = userId;
        Context.Items[DeviceIdItemKey] = deviceId;
        Context.Items[PlatformItemKey] = platform;

        var session = new PresenceSession(userId, deviceId, platform, PresenceStatus.Online,
            CustomActivity: null,
            ConnectedAt: now,
            LastHeartbeatAt: now,
            ConnectionId: Context.ConnectionId);
        var wasFirst = await sessions.AddSessionAsync(session, ct).ConfigureAwait(false);

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupForUser(userId), ct).ConfigureAwait(false);

        if (wasFirst)
        {
            await dispatcher.PublishUserCameOnlineAsync(userId, [platform], ct).ConfigureAwait(false);
        }

        await BroadcastPresenceAsync(userId, ct).ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(UserIdItemKey, out var userIdObj) && userIdObj is Guid userId
            && Context.Items.TryGetValue(DeviceIdItemKey, out var deviceIdObj) && deviceIdObj is string deviceId
            && Context.Items.TryGetValue(PlatformItemKey, out var platformObj) && platformObj is Platform platform)
        {
            var ct = CancellationToken.None;
            var (removed, wasLast) = await sessions
                .RemoveSessionForConnectionAsync(userId, deviceId, Context.ConnectionId, ct)
                .ConfigureAwait(false);

            if (removed && wasLast)
            {
                await PersistLastSeenAndPublishOfflineAsync(userId, platform, ct).ConfigureAwait(false);
            }

            await BroadcastPresenceAsync(userId, ct).ConfigureAwait(false);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    public Task Heartbeat()
    {
        var (userId, deviceId, _) = RequireConnectionContext();
        return sessions.RefreshHeartbeatAsync(userId, deviceId, clock.GetUtcNow(), Context.ConnectionAborted);
    }

    public async Task SetStatus(PresenceStatus status, string? customActivity)
    {
        var (userId, deviceId, _) = RequireConnectionContext();
        var ct = Context.ConnectionAborted;
        await sessions.UpdateSessionStatusAsync(userId, deviceId, status, customActivity, ct).ConfigureAwait(false);
        await BroadcastPresenceAsync(userId, ct).ConfigureAwait(false);
    }

    public async Task StartTyping(Guid conversationId)
    {
        var (userId, _, _) = RequireConnectionContext();
        var added = await typing.StartTypingAsync(conversationId, userId, Context.ConnectionAborted).ConfigureAwait(false);
        if (added)
        {
            await broadcaster.BroadcastTypingAsync(conversationId, userId, isTyping: true, Context.ConnectionAborted)
                .ConfigureAwait(false);
        }
    }

    public async Task StopTyping(Guid conversationId)
    {
        var (userId, _, _) = RequireConnectionContext();
        var removed = await typing.StopTypingAsync(conversationId, userId, Context.ConnectionAborted).ConfigureAwait(false);
        if (removed)
        {
            await broadcaster.BroadcastTypingAsync(conversationId, userId, isTyping: false, Context.ConnectionAborted)
                .ConfigureAwait(false);
        }
    }

    public async Task SubscribeToUsers(Guid[] userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        var ct = Context.ConnectionAborted;
        foreach (var userId in userIds.Distinct())
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupForUser(userId), ct).ConfigureAwait(false);
            await SendPresenceSnapshotToCallerAsync(userId, ct).ConfigureAwait(false);
        }
    }

    public Task UnsubscribeFromUsers(Guid[] userIds)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        var ct = Context.ConnectionAborted;
        return Task.WhenAll(userIds.Select(uid =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupForUser(uid), ct)));
    }

    private async Task PersistLastSeenAndPublishOfflineAsync(Guid userId, Platform platform, CancellationToken ct)
    {
        // Resolve repository per-call: the connection's DI scope ends with the
        // disconnect, so we open our own scope through HttpContext.RequestServices
        // would be wrong; use the hub's injected scoped service via a fresh scope.
        // Hub is scoped per-connection, but ILastSeenRepository is scoped — we
        // inject it directly so EF tracking is fine inside this method.
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

    private async Task SendPresenceSnapshotToCallerAsync(Guid userId, CancellationToken ct)
    {
        var userSessions = await sessions.GetSessionsAsync(userId, ct).ConfigureAwait(false);
        var ls = await lastSeen.GetAsync(userId, ct).ConfigureAwait(false);
        var aggregated = aggregator.Aggregate(userId, userSessions, ls?.LastSeenAt);
        var settings = await privacy.GetAsync(userId, ct).ConfigureAwait(false);
        var publicView = PrivacyFilter.Apply(aggregated, settings);
        await Clients.Caller.UserPresenceChanged(
            publicView.UserId,
            publicView.Status,
            publicView.Platforms.ToArray(),
            publicView.LastSeenAt).ConfigureAwait(false);
    }

    private (Guid UserId, string DeviceId, Platform Platform) RequireConnectionContext()
    {
        if (Context.Items[UserIdItemKey] is not Guid userId
            || Context.Items[DeviceIdItemKey] is not string deviceId
            || Context.Items[PlatformItemKey] is not Platform platform)
        {
            throw new HubException("Connection context not initialised");
        }
        return (userId, deviceId, platform);
    }

    private static Guid ResolveUserId(ClaimsPrincipal? user)
    {
        var sub = user?.FindFirstValue("sub")
            ?? throw new HubException("JWT does not contain sub claim");
        if (!Guid.TryParse(sub, out var id))
        {
            throw new HubException("sub claim is not a UUID");
        }
        return id;
    }

    private static string ResolveDeviceId(HubCallerContext ctx)
    {
        var fromClaim = ctx.User?.FindFirstValue("device_id");
        if (!string.IsNullOrEmpty(fromClaim)) return fromClaim;

        var fromQuery = ctx.GetHttpContext()?.Request.Query["deviceId"].ToString();
        if (!string.IsNullOrEmpty(fromQuery)) return fromQuery!;

        return Guid.NewGuid().ToString("N");
    }

    private static Platform ResolvePlatform(HubCallerContext ctx)
    {
        var raw = ctx.GetHttpContext()?.Request.Query["platform"].ToString();
        return Enum.TryParse<Platform>(raw, ignoreCase: true, out var p) ? p : Platform.Web;
    }
}
