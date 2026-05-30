using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Call;

public sealed class CallIncomingHandler :
    INotificationHandler<CallIncomingEvent>,
    INotificationHandler<CallIncomingV2Event>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        CallIncomingEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var label = integrationEvent.CallType == CallType.Video ? "Видеозвонок" : "Звонок";
        var content = NotificationContent.Create(
            "Входящий звонок",
            $"{label} от пользователя",
            imageUrl: null,
            deepLink: $"urfulink://call/{integrationEvent.CallId:N}/incoming");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["callId"] = integrationEvent.CallId.ToString("N", CultureInfo.InvariantCulture),
            ["callerId"] = integrationEvent.CallerId.ToString("N", CultureInfo.InvariantCulture),
            ["callType"] = integrationEvent.CallType.ToString(),
        });

        var groupKey = GroupKey.ForCall(integrationEvent.CallId);

        var drafts = new List<NotificationIntent>(integrationEvent.Recipients.Count);
        foreach (var recipientId in integrationEvent.Recipients)
        {
            if (recipientId == integrationEvent.CallerId)
            {
                continue;
            }

            drafts.Add(new NotificationIntent(
                RecipientUserId: recipientId,
                Category: NotificationCategory.CallIncoming,
                Severity: NotificationSeverity.Urgent,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.CallIncoming(integrationEvent.CallId),
                Priority: NotificationPriority.UrgentCall,
                Actor: new NotificationActor(integrationEvent.CallerId, null, null)));
        }

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(drafts);
    }

    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        CallIncomingV2Event integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var label = integrationEvent.CallType == CallType.Video ? "Видеозвонок" : "Звонок";
        var content = NotificationContent.Create(
            "Входящий звонок",
            $"{label} от пользователя",
            imageUrl: null,
            deepLink: $"urfulink://chat/conv/{integrationEvent.ConversationId}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["conversationId"] = integrationEvent.ConversationId,
            ["callId"] = integrationEvent.CallId.ToString("N", CultureInfo.InvariantCulture),
            ["callerId"] = integrationEvent.CallerId.ToString("N", CultureInfo.InvariantCulture),
            ["callType"] = integrationEvent.CallType.ToString(),
        });

        var groupKey = GroupKey.ForCall(integrationEvent.CallId);
        var drafts = new List<NotificationIntent>(integrationEvent.ParticipantIds.Count);
        foreach (var recipientId in integrationEvent.ParticipantIds)
        {
            if (recipientId == integrationEvent.CallerId)
            {
                continue;
            }

            drafts.Add(new NotificationIntent(
                RecipientUserId: recipientId,
                Category: NotificationCategory.CallIncoming,
                Severity: NotificationSeverity.Urgent,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.CallIncoming(integrationEvent.CallId),
                Priority: NotificationPriority.UrgentCall,
                Actor: new NotificationActor(integrationEvent.CallerId, null, null)));
        }

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(drafts);
    }
}
