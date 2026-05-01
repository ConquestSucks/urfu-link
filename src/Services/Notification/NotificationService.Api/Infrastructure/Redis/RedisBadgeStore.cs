using System.Globalization;
using StackExchange.Redis;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;

namespace Urfu.Link.Services.Notification.Infrastructure.Redis;

/// <summary>
/// Redis-backed badge counter using Lua scripts for atomic INCR/DECR pairs across the
/// total counter and the per-category counter.
/// </summary>
public sealed class RedisBadgeStore(IConnectionMultiplexer multiplexer) : IBadgeStore
{
    private readonly IConnectionMultiplexer _multiplexer = multiplexer;

    public async Task<int> IncrementAsync(Guid userId, NotificationCategory category, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var totalKey = (RedisKey)RedisKeys.BadgeTotal(userId);
        var perCategoryKey = (RedisKey)RedisKeys.BadgePerCategory(userId, (int)category);

        var db = _multiplexer.GetDatabase();
        var result = await db.ScriptEvaluateAsync(BadgeLuaScripts.Increment, [totalKey, perCategoryKey]).ConfigureAwait(false);
        return (int)result;
    }

    public async Task<int> DecrementAsync(Guid userId, NotificationCategory category, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var totalKey = (RedisKey)RedisKeys.BadgeTotal(userId);
        var perCategoryKey = (RedisKey)RedisKeys.BadgePerCategory(userId, (int)category);

        var db = _multiplexer.GetDatabase();
        var result = await db.ScriptEvaluateAsync(BadgeLuaScripts.Decrement, [totalKey, perCategoryKey]).ConfigureAwait(false);
        return (int)result;
    }

    public async Task ResetAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var endpoint = _multiplexer.GetEndPoints().FirstOrDefault();
        if (endpoint is null)
        {
            return;
        }

        var server = _multiplexer.GetServer(endpoint);
        var db = _multiplexer.GetDatabase();
        var pattern = RedisKeys.BadgePerCategoryPattern(userId);

        var keys = new List<RedisKey>();
        await foreach (var key in server.KeysAsync(pattern: pattern).ConfigureAwait(false))
        {
            keys.Add(key);
        }

        keys.Add(RedisKeys.BadgeTotal(userId));
        if (keys.Count > 0)
        {
            await db.KeyDeleteAsync([.. keys]).ConfigureAwait(false);
        }
    }

    public async Task<BadgeSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        var endpoint = _multiplexer.GetEndPoints().FirstOrDefault();
        if (endpoint is null)
        {
            return BadgeSnapshot.Empty;
        }

        var server = _multiplexer.GetServer(endpoint);
        var db = _multiplexer.GetDatabase();

        var totalRaw = await db.StringGetAsync(RedisKeys.BadgeTotal(userId)).ConfigureAwait(false);
        var total = totalRaw.HasValue && int.TryParse(totalRaw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTotal)
            ? parsedTotal
            : 0;

        var pattern = RedisKeys.BadgePerCategoryPattern(userId);
        var perCategory = new Dictionary<NotificationCategory, int>();

        await foreach (var key in server.KeysAsync(pattern: pattern).ConfigureAwait(false))
        {
            var keyName = key.ToString();
            var separator = keyName.LastIndexOf(':');
            if (separator < 0)
            {
                continue;
            }

            var suffix = keyName[(separator + 1)..];
            if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryNumber))
            {
                continue;
            }

            var raw = await db.StringGetAsync(key).ConfigureAwait(false);
            if (!raw.HasValue || !int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) || count <= 0)
            {
                continue;
            }

            perCategory[(NotificationCategory)categoryNumber] = count;
        }

        return new BadgeSnapshot(total, perCategory);
    }

    public async Task<int> SetSnapshotAsync(Guid userId, BadgeSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _ = cancellationToken;

        await ResetAsync(userId, cancellationToken).ConfigureAwait(false);

        var db = _multiplexer.GetDatabase();
        await db.StringSetAsync(
            RedisKeys.BadgeTotal(userId),
            snapshot.Total.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);

        foreach (var (category, count) in snapshot.PerCategory)
        {
            if (count <= 0)
            {
                continue;
            }

            await db.StringSetAsync(
                RedisKeys.BadgePerCategory(userId, (int)category),
                count.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        }

        return snapshot.Total;
    }
}
