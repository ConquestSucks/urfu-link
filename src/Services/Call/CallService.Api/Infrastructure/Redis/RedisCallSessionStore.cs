using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Urfu.Link.Services.Call.Application;
using Urfu.Link.Services.Call.Application.Calls;
using Urfu.Link.Services.Call.Domain;

namespace Urfu.Link.Services.Call.Infrastructure.Redis;

public sealed class RedisCallSessionStore(
    IConnectionMultiplexer redis,
    IOptions<CallOptions> options) : ICallSessionStore
{
    private const string KeyPrefix = "call:session:";
    private const string ExpiryIndexKey = "call:ring-expiry";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<bool> TryCreateAsync(CallSession session, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var db = redis.GetDatabase();
        var created = await db.StringSetAsync(
                SessionKey(session.Id),
                JsonSerializer.Serialize(session, JsonOptions),
                ttl,
                When.NotExists)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        if (created)
        {
            await db.SortedSetAddAsync(
                    ExpiryIndexKey,
                    session.Id.ToString("N"),
                    session.RingExpiresAtUtc.ToUnixTimeMilliseconds())
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return created;
    }

    public async Task<CallSession?> GetAsync(Guid callId, CancellationToken cancellationToken)
    {
        var payload = await redis.GetDatabase()
            .StringGetAsync(SessionKey(callId))
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        return payload.IsNullOrEmpty
            ? null
            : JsonSerializer.Deserialize<CallSession>(payload.ToString(), JsonOptions);
    }

    public async Task SaveAsync(CallSession session, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var db = redis.GetDatabase();
        await db.StringSetAsync(
                SessionKey(session.Id),
                JsonSerializer.Serialize(session, JsonOptions),
                ttl)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        if (session.Status == CallStatus.Ringing)
        {
            await db.SortedSetAddAsync(
                    ExpiryIndexKey,
                    session.Id.ToString("N"),
                    session.RingExpiresAtUtc.ToUnixTimeMilliseconds())
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await RemoveFromExpiryIndexAsync(session.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task RemoveFromExpiryIndexAsync(Guid callId, CancellationToken cancellationToken)
        => redis.GetDatabase()
            .SortedSetRemoveAsync(ExpiryIndexKey, callId.ToString("N"))
            .WaitAsync(cancellationToken);

    public async Task<IReadOnlyList<CallSession>> ListExpiredRingingAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var ids = await db.SortedSetRangeByScoreAsync(
                ExpiryIndexKey,
                stop: nowUtc.ToUnixTimeMilliseconds(),
                take: limit)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        if (ids.Length == 0)
        {
            return Array.Empty<CallSession>();
        }

        var sessions = new List<CallSession>(ids.Length);
        foreach (var idValue in ids)
        {
            if (!Guid.TryParseExact(idValue.ToString(), "N", out var id))
            {
                continue;
            }

            var session = await GetAsync(id, cancellationToken).ConfigureAwait(false);
            if (session?.Status == CallStatus.Ringing)
            {
                sessions.Add(session);
            }
            else
            {
                await RemoveFromExpiryIndexAsync(id, cancellationToken).ConfigureAwait(false);
            }
        }

        return sessions;
    }

    private static string SessionKey(Guid callId) => KeyPrefix + callId.ToString("N");
}
