using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

public sealed class ChatMessagePinnedHandler : INotificationHandler<ChatMessagePinnedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        ChatMessagePinnedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        if (integrationEvent.Recipients is null || integrationEvent.Recipients.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<NotificationIntent>>([]);
        }

        if (!Guid.TryParse(integrationEvent.ConversationId, out var conversationGuid))
        {
            conversationGuid = StableGuids.From(integrationEvent.ConversationId);
        }

        var content = NotificationContent.Create(
            "Сообщение закреплено",
            "В чате закрепили сообщение",
            imageUrl: null,
            deepLink: $"urfulink://chat/conv/{integrationEvent.ConversationId}/msg/{integrationEvent.MessageId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["conversationId"] = integrationEvent.ConversationId,
            ["messageId"] = integrationEvent.MessageId.ToString("N", CultureInfo.InvariantCulture),
            ["pinnedByUserId"] = integrationEvent.PinnedByUserId.ToString("N", CultureInfo.InvariantCulture),
        });

        var intents = new List<NotificationIntent>(integrationEvent.Recipients.Count);
        foreach (var recipientId in integrationEvent.Recipients)
        {
            if (recipientId == integrationEvent.PinnedByUserId)
            {
                continue;
            }

            intents.Add(new NotificationIntent(
                RecipientUserId: recipientId,
                Category: NotificationCategory.ChatMessagePinned,
                Severity: NotificationSeverity.Normal,
                Content: content,
                Data: data,
                GroupKey: GroupKey.ForDirectChat(conversationGuid),
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.ChatPin(integrationEvent.ConversationId, integrationEvent.MessageId),
                Priority: NotificationPriority.PinSystemAdmin,
                Actor: new NotificationActor(integrationEvent.PinnedByUserId, null, null),
                SuppressWhenViewingContextKey: NotificationViewingContexts.ChatConversation(integrationEvent.ConversationId)));
        }

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(intents);
    }
}
