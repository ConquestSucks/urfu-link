using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Chat;

/// <summary>
/// Maps <see cref="ChatMessageSentEvent"/> into per-recipient drafts. Recipients listed
/// in <see cref="ChatMessageSentEvent.Mentions"/> get a higher-severity Mention draft;
/// the remaining recipients receive a regular direct-message draft. The discriminator
/// between direct, mention, and discipline categories lives at the handler boundary.
/// </summary>
public sealed class ChatMessageSentHandler : INotificationHandler<ChatMessageSentEvent>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
        ChatMessageSentEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var drafts = new List<NotificationDraft>(integrationEvent.Recipients.Count);
        var mentions = new HashSet<Guid>(integrationEvent.Mentions ?? []);

        var conversationId = integrationEvent.ConversationId;
        if (!Guid.TryParse(conversationId, out var conversationGuid))
        {
            // Fallback: hash the string into a stable Guid so GroupKey remains deterministic.
            conversationGuid = StableGuids.From(conversationId);
        }

        var preview = string.IsNullOrWhiteSpace(integrationEvent.Preview)
            ? "Новое сообщение"
            : integrationEvent.Preview;

        foreach (var recipientId in integrationEvent.Recipients)
        {
            if (recipientId == integrationEvent.SenderId)
            {
                continue;
            }

            var isMention = mentions.Contains(recipientId);
            var category = isMention
                ? NotificationCategory.ChatMessageMention
                : NotificationCategory.ChatMessageDirect;
            var severity = isMention ? NotificationSeverity.High : NotificationSeverity.Normal;

            var groupKey = isMention
                ? GroupKey.ForChatMention(conversationGuid)
                : GroupKey.ForDirectChat(conversationGuid);

            var content = NotificationContent.Create(
                title: isMention ? "Вас упомянули" : "Новое сообщение",
                body: preview,
                imageUrl: null,
                deepLink: $"urfulink://chat/conv/{conversationId}/msg/{integrationEvent.MessageId:N}");

            var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["conversationId"] = conversationId,
                ["messageId"] = integrationEvent.MessageId.ToString("N", CultureInfo.InvariantCulture),
                ["senderId"] = integrationEvent.SenderId.ToString("N", CultureInfo.InvariantCulture),
            });

            drafts.Add(new NotificationDraft(
                RecipientUserId: recipientId,
                Category: category,
                Severity: severity,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType));
        }

        return Task.FromResult<IReadOnlyList<NotificationDraft>>(drafts);
    }

}
