using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

/// <summary>
/// Plain chat messages do not create notification-center items. Mentions are handled only
/// from <c>chat.mention.created.v1</c> so a message action cannot be downgraded to a
/// generic chat notification.
/// </summary>
public sealed class ChatMessageSentHandler : INotificationHandler<ChatMessageSentEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        ChatMessageSentEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;
        return Task.FromResult<IReadOnlyList<NotificationIntent>>(Array.Empty<NotificationIntent>());
    }
}
