using Urfu.Link.Services.Notification.Realtime;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Application.Services;

internal static class NotificationDtoMapper
{
    public static NotificationDto Map(NotificationAggregate notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return new NotificationDto(
            Id: notification.Id,
            RecipientUserId: notification.RecipientUserId,
            Category: notification.Category,
            Severity: notification.Severity,
            Title: notification.Content.Title,
            Body: notification.Content.Body,
            ImageUrl: notification.Content.ImageUrl,
            DeepLink: notification.Content.DeepLink,
            Data: notification.Data.Values,
            GroupKey: notification.GroupKey?.Value,
            CreatedAtUtc: notification.CreatedAtUtc,
            ReadAtUtc: notification.ReadAtUtc);
    }
}
