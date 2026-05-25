using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain;

public static class NotificationPriorityPolicy
{
    public static bool ShouldUpgrade(NotificationPriority existing, NotificationPriority candidate)
        => candidate > existing;
}
