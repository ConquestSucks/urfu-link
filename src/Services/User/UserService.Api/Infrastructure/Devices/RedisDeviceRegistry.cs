using StackExchange.Redis;
using UAParser;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Infrastructure.Devices;

public sealed class RedisDeviceRegistry(IConnectionMultiplexer redis) : IDeviceRegistry
{
    private static readonly Parser UaParser = Parser.GetDefault();
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(30);
    private const string KeyPrefix = "urfu:device:";
    private const string MappingPrefix = "urfu:session-map:";

    public async Task SaveAsync(string keycloakSessionId, string userAgent, CancellationToken cancellationToken = default)
    {
        var deviceName = ParseDeviceName(userAgent);
        var db = redis.GetDatabase();
        await db.StringSetAsync(KeyPrefix + keycloakSessionId, deviceName, Ttl)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> GetDeviceNameAsync(string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(KeyPrefix + keycloakSessionId)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task RemoveAsync(string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(KeyPrefix + keycloakSessionId)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveAllAsync(IEnumerable<string> keycloakSessionIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keycloakSessionIds);
        var db = redis.GetDatabase();
        var keys = keycloakSessionIds.Select(id => (RedisKey)(KeyPrefix + id)).ToArray();
        if (keys.Length > 0)
            await db.KeyDeleteAsync(keys)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
    }

    public async Task SavePomeriumMappingAsync(string pomeriumSid, string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        await db.StringSetAsync(MappingPrefix + pomeriumSid, keycloakSessionId, Ttl)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> GetKeycloakSessionIdAsync(string pomeriumSid, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(MappingPrefix + pomeriumSid)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    private static string ParseDeviceName(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Неизвестное устройство";

        var info = UaParser.Parse(userAgent);

        var os = info.OS.Family;
        var browser = info.UA.Family;
        var device = info.Device.Family;

        // Mobile device with known model
        if (!string.Equals(device, "Other", StringComparison.Ordinal) &&
            !string.Equals(device, "Generic Smartphone", StringComparison.Ordinal))
        {
            return $"{device}, {os}";
        }

        // Desktop/browser fallback
        if (!string.Equals(browser, "Other", StringComparison.Ordinal))
            return $"{browser}, {os}";

        return os is not null and not "Other" ? os : "Неизвестное устройство";
    }
}
