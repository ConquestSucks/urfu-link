using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Call;

public sealed class CallIncomingHandler : INotificationHandler<CallIncomingEvent>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
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

        var drafts = new List<NotificationDraft>(integrationEvent.Recipients.Count);
        foreach (var recipientId in integrationEvent.Recipients)
        {
            if (recipientId == integrationEvent.CallerId)
            {
                continue;
            }

            drafts.Add(new NotificationDraft(
                RecipientUserId: recipientId,
                Category: NotificationCategory.CallIncoming,
                Severity: NotificationSeverity.Urgent,
                Content: content,
                Data: data,
                GroupKey: groupKey,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType));
        }

        return Task.FromResult<IReadOnlyList<NotificationDraft>>(drafts);
    }
}
