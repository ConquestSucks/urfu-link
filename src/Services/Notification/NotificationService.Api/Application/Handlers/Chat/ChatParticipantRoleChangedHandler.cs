using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

public sealed class ChatParticipantRoleChangedHandler : INotificationHandler<ChatParticipantRoleChangedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        ChatParticipantRoleChangedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Роль в чате изменена",
            $"Ваша роль в чате изменена на {integrationEvent.NewRole}",
            imageUrl: null,
            deepLink: $"urfulink://chat/conv/{integrationEvent.ConversationId}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["conversationId"] = integrationEvent.ConversationId,
            ["oldRole"] = integrationEvent.OldRole.ToString(),
            ["newRole"] = integrationEvent.NewRole.ToString(),
        });

        var intent = new NotificationIntent(
            RecipientUserId: integrationEvent.UserId,
            Category: NotificationCategory.ChatParticipantChanged,
            Severity: NotificationSeverity.Normal,
            Content: content,
            Data: data,
            GroupKey: null,
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType,
            SourceActionId: NotificationSourceActions.ChatParticipant(integrationEvent.ConversationId, integrationEvent.UserId),
            Priority: NotificationPriority.PinSystemAdmin,
            SuppressWhenViewingContextKey: NotificationViewingContexts.ChatConversation(integrationEvent.ConversationId));

        return Task.FromResult<IReadOnlyList<NotificationIntent>>([intent]);
    }
}
