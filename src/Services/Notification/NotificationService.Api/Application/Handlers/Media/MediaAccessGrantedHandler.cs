using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Media;

public sealed class MediaAccessGrantedHandler : INotificationHandler<MediaAccessGrantedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        MediaAccessGrantedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Открыт доступ к файлу",
            "Вам открыли доступ к медиафайлу",
            imageUrl: null,
            deepLink: $"urfulink://media/{integrationEvent.AssetId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["assetId"] = integrationEvent.AssetId.ToString("N", CultureInfo.InvariantCulture),
            ["source"] = integrationEvent.Source.ToString(),
            ["sourceId"] = integrationEvent.SourceId ?? string.Empty,
        });

        var intents = integrationEvent.UserIds
            .Select(userId => new NotificationIntent(
                RecipientUserId: userId,
                Category: NotificationCategory.MediaAccessGranted,
                Severity: NotificationSeverity.Normal,
                Content: content,
                Data: data,
                GroupKey: null,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.MediaAsset(integrationEvent.AssetId, "access-granted"),
                Priority: NotificationPriority.PinSystemAdmin))
            .ToArray();

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(intents);
    }
}
