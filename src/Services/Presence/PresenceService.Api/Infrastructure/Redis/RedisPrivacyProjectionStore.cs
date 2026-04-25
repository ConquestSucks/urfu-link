using StackExchange.Redis;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Infrastructure.Redis;

public sealed class RedisPrivacyProjectionStore(IConnectionMultiplexer redis) : IPrivacyProjectionStore
{
    private const string OnlineField = "online";
    private const string LastVisitField = "lastVisit";

    public async Task<PrivacySettings> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var db = redis.GetDatabase();
        var key = RedisKeys.Privacy(userId);
        var entries = await db.HashGetAllAsync(key).ConfigureAwait(false);
        if (entries.Length == 0) return PrivacySettings.Default;

        var online = ReadFlag(entries, OnlineField, defaultValue: PrivacySettings.Default.ShowOnlineStatus);
        var lastVisit = ReadFlag(entries, LastVisitField, defaultValue: PrivacySettings.Default.ShowLastVisitTime);
        return new PrivacySettings(online, lastVisit);
    }

    public async Task SetAsync(Guid userId, PrivacySettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();

        var db = redis.GetDatabase();
        var key = RedisKeys.Privacy(userId);
        await db.HashSetAsync(key,
            [
                new HashEntry(OnlineField, settings.ShowOnlineStatus ? "1" : "0"),
                new HashEntry(LastVisitField, settings.ShowLastVisitTime ? "1" : "0"),
            ]).ConfigureAwait(false);
    }

    private static bool ReadFlag(HashEntry[] entries, string field, bool defaultValue)
    {
        foreach (var e in entries)
        {
            if (e.Name == field) return (string?)e.Value == "1";
        }
        return defaultValue;
    }
}
