using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Urfu.Link.BuildingBlocks.Idempotency;

/// <summary>
/// Per-key fixed-window rate limiter. Each successful <see cref="TryAcquireAsync"/> consumes
/// one token; once <see cref="RateLimiterOptions.MaxRequests"/> is exceeded inside the window,
/// further calls return <see cref="RateLimitDecision.Allowed"/> = false until the window
/// elapses.
/// </summary>
public interface IRateLimiter
{
    ValueTask<RateLimitDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record RateLimitDecision(bool Allowed, TimeSpan? RetryAfter);

public sealed class RateLimiterOptions
{
    public string Name { get; set; } = string.Empty;

    public TimeSpan Window { get; set; }

    public int MaxRequests { get; set; }

    public string KeyPrefix { get; set; } = "urfu:rate-limit";
}

/// <summary>
/// Redis-backed fixed-window limiter. The Lua script is atomic — INCR is paired with PEXPIRE
/// only on the first request of a window, so process death between ops cannot leave a key
/// without a TTL.
/// </summary>
public sealed class RedisFixedWindowRateLimiter(
    IConnectionMultiplexer multiplexer,
    RateLimiterOptions options) : IRateLimiter
{
    private const string IncrementWithExpireScript =
        "local count = redis.call('INCR', KEYS[1]) " +
        "if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end " +
        "return count";

    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var database = multiplexer.GetDatabase();
        var redisKey = (RedisKey)$"{options.KeyPrefix}:{options.Name}:{key}";
        var ttlMs = (long)options.Window.TotalMilliseconds;

        var raw = await database.ScriptEvaluateAsync(
            IncrementWithExpireScript,
            new[] { redisKey },
            new RedisValue[] { ttlMs }).WaitAsync(cancellationToken).ConfigureAwait(false);

        var count = (long)raw;

        if (count <= options.MaxRequests)
        {
            return new RateLimitDecision(Allowed: true, RetryAfter: null);
        }

        var pttl = await database.KeyTimeToLiveAsync(redisKey).WaitAsync(cancellationToken).ConfigureAwait(false);
        return new RateLimitDecision(Allowed: false, RetryAfter: pttl ?? options.Window);
    }
}

public static class RateLimiterServiceCollectionExtensions
{
    /// <summary>
    /// Registers a Redis-backed fixed-window rate limiter as a keyed singleton. Resolve with
    /// <c>[FromKeyedServices(name)] IRateLimiter</c>. Multiple policies coexist; each gets its
    /// own Redis namespace via <see cref="RateLimiterOptions.Name"/>.
    /// </summary>
    public static IServiceCollection AddRedisRateLimiter(
        this IServiceCollection services,
        string name,
        TimeSpan window,
        int maxRequests,
        string keyPrefix = "urfu:rate-limit")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be positive.");
        }
        if (maxRequests <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "MaxRequests must be positive.");
        }

        services.AddKeyedSingleton<IRateLimiter>(name, (sp, _) =>
        {
            var multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisFixedWindowRateLimiter(multiplexer, new RateLimiterOptions
            {
                Name = name,
                Window = window,
                MaxRequests = maxRequests,
                KeyPrefix = keyPrefix,
            });
        });
        return services;
    }
}
