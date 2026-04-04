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
    private const string ReverseMappingPrefix = "urfu:session-revmap:";

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
        var pomeriumSid = await db.StringGetAsync(ReverseMappingPrefix + keycloakSessionId)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        var keys = new List<RedisKey>
        {
            KeyPrefix + keycloakSessionId,
            ReverseMappingPrefix + keycloakSessionId,
        };
        if (pomeriumSid.HasValue)
            keys.Add(MappingPrefix + pomeriumSid.ToString());

        await db.KeyDeleteAsync(keys.ToArray())
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveAllAsync(IEnumerable<string> keycloakSessionIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keycloakSessionIds);
        var db = redis.GetDatabase();
        var ids = keycloakSessionIds.ToList();
        if (ids.Count == 0)
            return;

        var pomeriumSids = await Task.WhenAll(
            ids.Select(id => db.StringGetAsync(ReverseMappingPrefix + id))
        ).WaitAsync(cancellationToken).ConfigureAwait(false);

        var keys = new List<RedisKey>();
        foreach (var id in ids)
        {
            keys.Add(KeyPrefix + id);
            keys.Add(ReverseMappingPrefix + id);
        }
        foreach (var sid in pomeriumSids)
        {
            if (sid.HasValue)
                keys.Add(MappingPrefix + sid.ToString());
        }

        await db.KeyDeleteAsync(keys.ToArray())
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SavePomeriumMappingAsync(string pomeriumSid, string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var batch = db.CreateBatch();
        var t1 = batch.StringSetAsync(MappingPrefix + pomeriumSid, keycloakSessionId, Ttl);
        var t2 = batch.StringSetAsync(ReverseMappingPrefix + keycloakSessionId, pomeriumSid, Ttl);
        batch.Execute();
        await Task.WhenAll(t1, t2).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetKeycloakSessionIdAsync(string pomeriumSid, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(MappingPrefix + pomeriumSid)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<string?> GetPomeriumSidByKeycloakSessionAsync(string keycloakSessionId, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(ReverseMappingPrefix + keycloakSessionId)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    internal static string ParseDeviceName(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Неизвестное устройство";

        var info = UaParser.Parse(userAgent);

        var os = info.OS.Family;
        var browser = info.UA.Family;
        var device = info.Device.Family;

        // Mobile device with known model (exclude generic desktop identifiers and
        // single-letter placeholders used by Chrome UA Reduction on Android, e.g. "K")
        var isDesktopDevice = string.Equals(device, "Other", StringComparison.Ordinal)
            || string.Equals(device, "Generic Smartphone", StringComparison.Ordinal)
            || string.Equals(device, "Mac", StringComparison.Ordinal)
            || string.Equals(device, "PC", StringComparison.Ordinal)
            || device.Length == 1;

        if (!isDesktopDevice)
            return $"{device}, {os}";

        // Desktop/browser fallback
        if (!string.Equals(browser, "Other", StringComparison.Ordinal))
            return $"{browser}, {os}";

        return os is not null and not "Other" ? os : "Неизвестное устройство";
    }
}
