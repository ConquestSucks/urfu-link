using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Admin;

/// <summary>
/// When a user is added to a chat conversation by another participant we surface it as
/// an "AdminChatInvite" notification — the recipient gets a normal-severity ping.
/// </summary>
public sealed class AdminChatInviteHandler : INotificationHandler<ChatParticipantJoinedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        ChatParticipantJoinedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Вас добавили в чат",
            "Вы стали участником беседы",
            imageUrl: null,
            deepLink: $"urfulink://chat/conv/{integrationEvent.ConversationId}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["conversationId"] = integrationEvent.ConversationId,
        });

        var draft = new NotificationIntent(
            RecipientUserId: integrationEvent.UserId,
            Category: NotificationCategory.AdminChatInvite,
            Severity: NotificationSeverity.Normal,
            Content: content,
            Data: data,
            GroupKey: null,
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType);

        return Task.FromResult<IReadOnlyList<NotificationIntent>>([draft]);
    }
}
