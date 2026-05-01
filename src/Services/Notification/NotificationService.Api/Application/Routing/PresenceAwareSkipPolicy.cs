using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Application.Routing;

/// <summary>
/// Decides whether Push delivery should be suppressed because the recipient is
/// already actively receiving the same notification in-app.
/// Only applies to chat categories — calls and system events bypass this even when
/// the user is on web.
/// </summary>
internal static class PresenceAwareSkipPolicy
{
    public static bool ShouldSkipPush(NotificationCategory category, NotificationSeverity severity, bool userOnlineOnWeb)
    {
        if (!userOnlineOnWeb)
        {
            return false;
        }

        // Urgent always reaches the device — calls and emergencies bypass presence.
        if (severity == NotificationSeverity.Urgent)
        {
            return false;
        }

        return category is NotificationCategory.ChatMessageDirect
            or NotificationCategory.ChatMessageMention
            or NotificationCategory.ChatMessageDiscipline;
    }
}
