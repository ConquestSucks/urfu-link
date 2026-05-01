using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Application.Routing;

/// <summary>
/// Maps a severity level to the candidate set of channels that may carry the notification
/// before per-user preferences are applied.
/// </summary>
public static class SeverityRouter
{
    public static IReadOnlyList<DeliveryChannel> Select(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Low => [DeliveryChannel.InApp],
        NotificationSeverity.Normal => [DeliveryChannel.InApp, DeliveryChannel.Push],
        NotificationSeverity.High => [DeliveryChannel.InApp, DeliveryChannel.Push, DeliveryChannel.Email],
        NotificationSeverity.Urgent => [DeliveryChannel.InApp, DeliveryChannel.Push, DeliveryChannel.Email],
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown severity."),
    };
}
