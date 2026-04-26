namespace Urfu.Link.Services.Notification.Infrastructure.Redis;

internal static class BadgeLuaScripts
{
    public const string Increment = """
        local total = redis.call('INCR', KEYS[1])
        redis.call('INCR', KEYS[2])
        return total
        """;

    public const string Decrement = """
        local current = tonumber(redis.call('GET', KEYS[1]) or '0')
        if current > 0 then redis.call('DECR', KEYS[1]) end
        local currentCat = tonumber(redis.call('GET', KEYS[2]) or '0')
        if currentCat > 0 then redis.call('DECR', KEYS[2]) end
        return math.max(current - 1, 0)
        """;
}
