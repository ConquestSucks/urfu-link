using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

/// <summary>
/// Maps <see cref="ChatMessageSentEvent"/> into per-recipient intents. Mentions are
/// intentionally skipped here and are handled only from <c>chat.mention.created.v1</c>
/// so one message action cannot create a generic and mention notification for the same user.
/// </summary>
public sealed class ChatMessageSentHandler(IDisciplineConversationLookup disciplineLookup)
    : INotificationHandler<ChatMessageSentEvent>
{
    public async Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        ChatMessageSentEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var drafts = new List<NotificationIntent>(integrationEvent.Recipients.Count);
        var mentions = new HashSet<Guid>(integrationEvent.Mentions ?? []);

        var conversationId = integrationEvent.ConversationId;
        if (!Guid.TryParse(conversationId, out var conversationGuid))
        {
            conversationGuid = StableGuids.From(conversationId);
        }

        var isDisciplineConversation = await disciplineLookup
            .IsDisciplineConversationAsync(conversationId, cancellationToken)
            .ConfigureAwait(false);

        var preview = string.IsNullOrWhiteSpace(integrationEvent.Preview)
            ? "Новое сообщение"
            : integrationEvent.Preview;

        foreach (var recipientId in integrationEvent.Recipients)
        {
            if (recipientId == integrationEvent.SenderId)
            {
                continue;
            }

            if (mentions.Contains(recipientId))
            {
                continue;
            }

            NotificationCategory category;
            NotificationSeverity severity;
            string title;
            GroupKey groupKey;

            if (isDisciplineConversation)
            {
                category = NotificationCategory.ChatMessageDiscipline;
                severity = NotificationSeverity.Normal;
                title = "Сообщение в дисциплине";
                groupKey = GroupKey.ForDisciplineChat(conversationGuid);
            }
            else
            {
                category = NotificationCategory.ChatMessageDirect;
                severity = NotificationSeverity.Normal;
                title = "Новое сообщение";
                groupKey = GroupKey.ForDirectChat(conversationGuid);
            }

            var content = NotificationContent.Create(
                title: title,
                body: preview,
                imageUrl: null,
                deepLink: $"urfulink://chat/conv/{conversationId}/msg/{integrationEvent.MessageId:N}");

            var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["conversationId"] = conversationId,
                ["messageId"] = integrationEvent.MessageId.ToString("N", CultureInfo.InvariantCulture),
                ["senderId"] = integrationEvent.SenderId.ToString("N", CultureInfo.InvariantCulture),
            });

            drafts.Add(new NotificationIntent(
                RecipientUserId: recipientId,
                Category: category,
                Severity: severity,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.ChatMessage(conversationId, integrationEvent.MessageId),
                Priority: NotificationPriority.ChatMessage,
                Actor: new NotificationActor(integrationEvent.SenderId, null, null),
                SuppressWhenViewingContextKey: NotificationViewingContexts.ChatConversation(conversationId)));
        }

        return drafts;
    }
}
