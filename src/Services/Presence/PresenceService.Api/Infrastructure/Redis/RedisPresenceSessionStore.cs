using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Infrastructure.Redis;

internal sealed record StoredSession(
    string DeviceId,
    Platform Platform,
    PresenceStatus Status,
    string? CustomActivity,
    DateTimeOffset ConnectedAt,
    DateTimeOffset LastHeartbeatAt);

public sealed class RedisPresenceSessionStore : IPresenceSessionStore
{
    private const string AddScript = """
        local existedBefore = redis.call('HLEN', KEYS[1])
        redis.call('HSET', KEYS[1], ARGV[1], ARGV[2])
        redis.call('EXPIRE', KEYS[1], ARGV[4])
        redis.call('ZADD', KEYS[2], ARGV[3], ARGV[5])
        return existedBefore
        """;

    private const string RemoveScript = """
        local removed = redis.call('HDEL', KEYS[1], ARGV[1])
        redis.call('ZREM', KEYS[2], ARGV[2])
        local remaining = redis.call('HLEN', KEYS[1])
        if remaining == 0 then redis.call('DEL', KEYS[1]) end
        return {removed, remaining}
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _sessionTtl;

    public RedisPresenceSessionStore(
        IConnectionMultiplexer redis,
        TimeProvider clock,
        IOptions<PresenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _redis = redis;
        _clock = clock;
        _sessionTtl = options.Value.SessionTtl;
    }

    public async Task<bool> AddSessionAsync(PresenceSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var sessionsKey = RedisKeys.UserSessions(session.UserId);
        var stored = new StoredSession(
            session.DeviceId,
            session.Platform,
            session.Status,
            session.CustomActivity,
            session.ConnectedAt,
            session.LastHeartbeatAt);
        var json = JsonSerializer.Serialize(stored, JsonOptions);
        var nowMs = session.LastHeartbeatAt.ToUnixTimeMilliseconds();
        var member = RedisKeys.HeartbeatMember(session.UserId, session.DeviceId);

        var result = (long)await db.ScriptEvaluateAsync(
            AddScript,
            keys: [sessionsKey, RedisKeys.Heartbeats],
            values:
            [
                session.DeviceId,
                json,
                nowMs,
                (long)_sessionTtl.TotalSeconds,
                member,
            ]).ConfigureAwait(false);

        return result == 0;
    }

    public async Task<(bool Removed, bool WasLast)> RemoveSessionAsync(
        Guid userId, string deviceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var sessionsKey = RedisKeys.UserSessions(userId);
        var member = RedisKeys.HeartbeatMember(userId, deviceId);

        var result = (RedisResult[])(await db.ScriptEvaluateAsync(
            RemoveScript,
            keys: [sessionsKey, RedisKeys.Heartbeats],
            values: [deviceId, member]).ConfigureAwait(false))!;

        var removed = (long)result[0] == 1;
        var remaining = (long)result[1];
        return (removed, removed && remaining == 0);
    }

    public async Task RefreshHeartbeatAsync(
        Guid userId, string deviceId, DateTimeOffset utcNow, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var sessionsKey = RedisKeys.UserSessions(userId);
        var raw = await db.HashGetAsync(sessionsKey, deviceId).ConfigureAwait(false);
        if (raw.IsNullOrEmpty) return;

        var stored = JsonSerializer.Deserialize<StoredSession>((string)raw!, JsonOptions);
        if (stored is null) return;

        var refreshed = stored with { LastHeartbeatAt = utcNow };
        var json = JsonSerializer.Serialize(refreshed, JsonOptions);
        var member = RedisKeys.HeartbeatMember(userId, deviceId);

        var batch = db.CreateBatch();
        var hset = batch.HashSetAsync(sessionsKey, deviceId, json);
        var expire = batch.KeyExpireAsync(sessionsKey, _sessionTtl);
        var zadd = batch.SortedSetAddAsync(RedisKeys.Heartbeats, member, utcNow.ToUnixTimeMilliseconds());
        batch.Execute();
        await Task.WhenAll(hset, expire, zadd).ConfigureAwait(false);
    }

    public async Task UpdateSessionStatusAsync(
        Guid userId,
        string deviceId,
        PresenceStatus status,
        string? customActivity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var sessionsKey = RedisKeys.UserSessions(userId);
        var raw = await db.HashGetAsync(sessionsKey, deviceId).ConfigureAwait(false);
        if (raw.IsNullOrEmpty) return;

        var stored = JsonSerializer.Deserialize<StoredSession>((string)raw!, JsonOptions);
        if (stored is null) return;

        var updated = stored with { Status = status, CustomActivity = customActivity };
        var json = JsonSerializer.Serialize(updated, JsonOptions);
        await db.HashSetAsync(sessionsKey, deviceId, json).ConfigureAwait(false);
        await db.KeyExpireAsync(sessionsKey, _sessionTtl).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PresenceSession>> GetSessionsAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        var sessionsKey = RedisKeys.UserSessions(userId);
        var entries = await db.HashGetAllAsync(sessionsKey).ConfigureAwait(false);
        if (entries.Length == 0) return [];

        var sessions = new List<PresenceSession>(entries.Length);
        foreach (var entry in entries)
        {
            if (entry.Value.IsNullOrEmpty) continue;
            var stored = JsonSerializer.Deserialize<StoredSession>((string)entry.Value!, JsonOptions);
            if (stored is null) continue;
            sessions.Add(new PresenceSession(
                userId,
                stored.DeviceId,
                stored.Platform,
                stored.Status,
                stored.CustomActivity,
                stored.ConnectedAt,
                stored.LastHeartbeatAt));
        }
        return sessions;
    }

    public async Task<IReadOnlyList<(Guid UserId, string DeviceId)>> GetExpiredSessionsAsync(
        DateTimeOffset cutoffUtc, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        cancellationToken.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var members = await db.SortedSetRangeByScoreAsync(
            RedisKeys.Heartbeats,
            start: double.NegativeInfinity,
            stop: cutoffUtc.ToUnixTimeMilliseconds(),
            exclude: Exclude.None,
            order: Order.Ascending,
            skip: 0,
            take: limit).ConfigureAwait(false);

        if (members.Length == 0) return [];
        var parsed = new List<(Guid, string)>(members.Length);
        foreach (var member in members)
        {
            var s = (string?)member;
            if (string.IsNullOrEmpty(s)) continue;
            var p = RedisKeys.TryParseHeartbeatMember(s);
            if (p is { } v) parsed.Add(v);
        }
        return parsed;
    }
}
