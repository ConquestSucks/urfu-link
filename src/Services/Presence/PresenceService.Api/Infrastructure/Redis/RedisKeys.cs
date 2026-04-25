using System.Globalization;

namespace Urfu.Link.Services.Presence.Infrastructure.Redis;

internal static class RedisKeys
{
    public static string UserSessions(Guid userId) =>
        string.Create(CultureInfo.InvariantCulture, $"presence:user:{userId:N}:sessions");

    public const string Heartbeats = "presence:heartbeats";

    public static string TypingMember(Guid conversationId, Guid userId) =>
        string.Create(CultureInfo.InvariantCulture, $"presence:typing:{conversationId:N}:{userId:N}");

    public static string Privacy(Guid userId) =>
        string.Create(CultureInfo.InvariantCulture, $"presence:privacy:{userId:N}");

    public static string HeartbeatMember(Guid userId, string deviceId) =>
        string.Create(CultureInfo.InvariantCulture, $"{userId:N}:{deviceId}");

    public static (Guid UserId, string DeviceId)? TryParseHeartbeatMember(string member)
    {
        var sep = member.IndexOf(':', StringComparison.Ordinal);
        if (sep <= 0 || sep == member.Length - 1) return null;
        if (!Guid.TryParseExact(member[..sep], "N", out var userId)) return null;
        return (userId, member[(sep + 1)..]);
    }
}
