using System.Globalization;

namespace Urfu.Link.Services.Notification.Application.Routing;

internal static class NotificationSourceActions
{
    public static string ChatMessage(string conversationId, Guid messageId)
        => $"chat:message:{conversationId}:{messageId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string ChatThreadReply(string conversationId, Guid messageId)
        => $"chat:thread-reply:{conversationId}:{messageId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string ChatReaction(string conversationId, Guid messageId, Guid reactorId, string emoji)
        => $"chat:reaction:{conversationId}:{messageId.ToString("N", CultureInfo.InvariantCulture)}:{reactorId.ToString("N", CultureInfo.InvariantCulture)}:{emoji}";

    public static string ChatPin(string conversationId, Guid messageId)
        => $"chat:pin:{conversationId}:{messageId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string ChatParticipant(string conversationId, Guid userId)
        => $"chat:participant:{conversationId}:{userId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string CallIncoming(Guid callId)
        => $"call:incoming:{callId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string CallMissed(Guid callId, Guid recipientId)
        => $"call:missed:{callId.ToString("N", CultureInfo.InvariantCulture)}:{recipientId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string DisciplineUser(Guid disciplineId, Guid userId, string action)
        => $"discipline:{action}:{disciplineId.ToString("N", CultureInfo.InvariantCulture)}:{userId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string DisciplineItem(Guid disciplineId, Guid itemId, string action)
        => $"discipline:{action}:{disciplineId.ToString("N", CultureInfo.InvariantCulture)}:{itemId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string MediaAsset(Guid assetId, string action)
        => $"media:{action}:{assetId.ToString("N", CultureInfo.InvariantCulture)}";

    public static string Fallback(Guid recipientUserId, Guid sourceEventId, string sourceEventType)
        => $"{sourceEventType}:{sourceEventId.ToString("N", CultureInfo.InvariantCulture)}:{recipientUserId.ToString("N", CultureInfo.InvariantCulture)}";
}
