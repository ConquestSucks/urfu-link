using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Urfu.Link.Services.Presence.Domain.Interfaces;

namespace Urfu.Link.Services.Presence.Infrastructure.Redis;

// We model typing as one Redis string per (conversationId, userId) with TTL,
// rather than a Redis SET, because Redis sets don't support per-member expiry
// and the issue requires auto-expire on individual typing entries.
public sealed class RedisTypingStore : ITypingStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _ttl;

    public RedisTypingStore(IConnectionMultiplexer redis, IOptions<PresenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _redis = redis;
        _ttl = options.Value.TypingTtl;
    }

    public Task<bool> StartTypingAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        var key = RedisKeys.TypingMember(conversationId, userId);
        return db.StringSetAsync(key, "1", _ttl, when: When.NotExists);
    }

    public async Task<bool> StopTypingAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        var key = RedisKeys.TypingMember(conversationId, userId);
        return await db.KeyDeleteAsync(key).ConfigureAwait(false);
    }

    public async Task<bool> IsTypingAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        var key = RedisKeys.TypingMember(conversationId, userId);
        return await db.KeyExistsAsync(key).ConfigureAwait(false);
    }
}
