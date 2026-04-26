namespace Urfu.Link.Services.Notification.Infrastructure.Redis;

public static class RedisKeys
{
    public const string BadgeTotalPrefix = "urfu:notif:badge:";

    public static string BadgeTotal(Guid userId) => $"{BadgeTotalPrefix}{userId:N}";

    public static string BadgePerCategory(Guid userId, int category) => $"{BadgeTotalPrefix}{userId:N}:cat:{category}";

    public static string BadgePerCategoryPattern(Guid userId) => $"{BadgeTotalPrefix}{userId:N}:cat:*";

    public static string PreferencesCache(Guid userId) => $"urfu:notif:prefs:{userId:N}";
}
