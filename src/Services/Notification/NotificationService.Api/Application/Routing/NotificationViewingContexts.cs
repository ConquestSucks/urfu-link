using System.Globalization;

namespace Urfu.Link.Services.Notification.Application.Routing;

internal static class NotificationViewingContexts
{
    public static string ChatConversation(string conversationId) =>
        $"chat:conversation:{conversationId}";

    public static string ChatThread(string conversationId, Guid rootMessageId) =>
        $"chat:thread:{conversationId}:{rootMessageId.ToString("N", CultureInfo.InvariantCulture)}";
}
