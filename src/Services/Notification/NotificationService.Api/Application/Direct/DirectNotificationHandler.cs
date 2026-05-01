using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Direct;

public sealed class DirectNotificationHandler : INotificationHandler<DirectNotificationCommand>
{
    public Task<IReadOnlyList<NotificationDraft>> PrepareAsync(
        DirectNotificationCommand integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            integrationEvent.Title,
            integrationEvent.Body,
            imageUrl: null,
            deepLink: integrationEvent.DeepLink);

        var data = NotificationData.From(integrationEvent.Data);

        var draft = new NotificationDraft(
            RecipientUserId: integrationEvent.RecipientUserId,
            Category: integrationEvent.Category,
            Severity: integrationEvent.Severity,
            Content: content,
            Data: data,
            GroupKey: null,
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType);

        return Task.FromResult<IReadOnlyList<NotificationDraft>>([draft]);
    }
}
