using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Call;

public sealed class CallMissedHandler : INotificationHandler<CallMissedEvent>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
        CallMissedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var label = integrationEvent.CallType == CallType.Video ? "Видеозвонок" : "Звонок";
        var content = NotificationContent.Create(
            "Пропущенный звонок",
            $"{label} остался без ответа",
            imageUrl: null,
            deepLink: $"urfulink://call/{integrationEvent.CallId:N}/missed");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["callId"] = integrationEvent.CallId.ToString("N", CultureInfo.InvariantCulture),
            ["callerId"] = integrationEvent.CallerId.ToString("N", CultureInfo.InvariantCulture),
            ["ringSeconds"] = ((int)integrationEvent.RingDuration.TotalSeconds).ToString(CultureInfo.InvariantCulture),
        });

        var draft = new NotificationDraft(
            RecipientUserId: integrationEvent.RecipientId,
            Category: NotificationCategory.CallMissed,
            Severity: NotificationSeverity.Normal,
            Content: content,
            Data: data,
            GroupKey: GroupKey.ForCall(integrationEvent.CallId),
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType);

        return Task.FromResult<IReadOnlyList<NotificationDraft>>([draft]);
    }
}
