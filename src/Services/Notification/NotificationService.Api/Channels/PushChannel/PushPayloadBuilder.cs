using Urfu.Link.Services.Notification.Domain.Aggregates;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Channels.PushChannel;

internal static class PushPayloadBuilder
{
    public static PushPayload For(NotificationAggregate notification, Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(delivery);

        return new PushPayload(
            Token: delivery.Address,
            Title: notification.Content.Title,
            Body: notification.Content.Body,
            ImageUrl: notification.Content.ImageUrl,
            DeepLink: notification.Content.DeepLink,
            GroupKey: notification.GroupKey?.Value,
            Data: notification.Data.Values);
    }
}
