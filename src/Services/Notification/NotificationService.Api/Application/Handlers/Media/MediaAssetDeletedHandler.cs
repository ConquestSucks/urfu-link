using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Media;

public sealed class MediaAssetDeletedHandler : INotificationHandler<MediaAssetDeletedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        MediaAssetDeletedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Файл удален",
            "Медиафайл был удален",
            imageUrl: null,
            deepLink: $"urfulink://media/{integrationEvent.AssetId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["assetId"] = integrationEvent.AssetId.ToString("N", CultureInfo.InvariantCulture),
            ["deletedAtUtc"] = integrationEvent.DeletedAtUtc.ToString("o", CultureInfo.InvariantCulture),
        });

        var intent = new NotificationIntent(
            RecipientUserId: integrationEvent.OwnerId,
            Category: NotificationCategory.MediaAssetDeleted,
            Severity: NotificationSeverity.High,
            Content: content,
            Data: data,
            GroupKey: null,
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType,
            SourceActionId: NotificationSourceActions.MediaAsset(integrationEvent.AssetId, "deleted"),
            Priority: NotificationPriority.PinSystemAdmin);

        return Task.FromResult<IReadOnlyList<NotificationIntent>>([intent]);
    }
}
