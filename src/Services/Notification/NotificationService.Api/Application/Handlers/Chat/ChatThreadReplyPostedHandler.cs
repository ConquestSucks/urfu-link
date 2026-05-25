using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

public sealed class ChatThreadReplyPostedHandler : INotificationHandler<ChatThreadReplyPostedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        ChatThreadReplyPostedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var mentioned = new HashSet<Guid>(integrationEvent.Mentions ?? []);
        var intents = new List<NotificationIntent>(integrationEvent.Subscribers.Count);
        if (!Guid.TryParse(integrationEvent.ConversationId, out var conversationGuid))
        {
            conversationGuid = StableGuids.From(integrationEvent.ConversationId);
        }

        foreach (var recipientId in integrationEvent.Subscribers)
        {
            if (recipientId == integrationEvent.SenderId || mentioned.Contains(recipientId))
            {
                continue;
            }

            var replyToMe = integrationEvent.RootMessageAuthorId == recipientId;
            var category = replyToMe ? NotificationCategory.ChatReplyToMe : NotificationCategory.ChatThreadReply;
            var severity = replyToMe ? NotificationSeverity.High : NotificationSeverity.Normal;
            var title = replyToMe ? "Ответ на ваше сообщение" : "Новый ответ в ветке";

            var content = NotificationContent.Create(
                title,
                "В ветке появился новый ответ",
                imageUrl: null,
                deepLink: $"urfulink://chat/conv/{integrationEvent.ConversationId}/thread/{integrationEvent.RootMessageId:N}/msg/{integrationEvent.MessageId:N}");

            var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["conversationId"] = integrationEvent.ConversationId,
                ["rootMessageId"] = integrationEvent.RootMessageId.ToString("N", CultureInfo.InvariantCulture),
                ["messageId"] = integrationEvent.MessageId.ToString("N", CultureInfo.InvariantCulture),
                ["senderId"] = integrationEvent.SenderId.ToString("N", CultureInfo.InvariantCulture),
            });

            intents.Add(new NotificationIntent(
                RecipientUserId: recipientId,
                Category: category,
                Severity: severity,
                Content: content,
                Data: data,
                GroupKey: GroupKey.ForChatMention(conversationGuid),
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.ChatThreadReply(integrationEvent.ConversationId, integrationEvent.MessageId),
                Priority: replyToMe ? NotificationPriority.ReplyToMe : NotificationPriority.ThreadReply,
                Actor: new NotificationActor(integrationEvent.SenderId, null, null),
                SuppressWhenViewingContextKey: NotificationViewingContexts.ChatThread(
                    integrationEvent.ConversationId,
                    integrationEvent.RootMessageId)));
        }

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(intents);
    }
}
