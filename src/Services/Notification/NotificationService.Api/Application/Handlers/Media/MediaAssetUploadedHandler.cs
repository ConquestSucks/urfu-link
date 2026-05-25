using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Media;

public sealed class MediaAssetUploadedHandler : INotificationHandler<MediaAssetUploadedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        MediaAssetUploadedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Файл загружен",
            integrationEvent.OriginalFileName,
            imageUrl: null,
            deepLink: $"urfulink://media/{integrationEvent.AssetId:N}");

        var data = NotificationData.From(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["assetId"] = integrationEvent.AssetId.ToString("N", CultureInfo.InvariantCulture),
            ["kind"] = integrationEvent.Kind.ToString(),
            ["size"] = integrationEvent.Size.ToString(CultureInfo.InvariantCulture),
            ["mimeType"] = integrationEvent.MimeType,
            ["fileName"] = integrationEvent.OriginalFileName,
        });

        var intent = new NotificationIntent(
            RecipientUserId: integrationEvent.OwnerId,
            Category: NotificationCategory.MediaUploadProcessed,
            Severity: NotificationSeverity.Normal,
            Content: content,
            Data: data,
            GroupKey: null,
            SourceEventId: integrationEvent.EventId,
            SourceEventType: integrationEvent.EventType,
            SourceActionId: NotificationSourceActions.MediaAsset(integrationEvent.AssetId, "upload"),
            Priority: NotificationPriority.PinSystemAdmin);

        return Task.FromResult<IReadOnlyList<NotificationIntent>>([intent]);
    }
}
