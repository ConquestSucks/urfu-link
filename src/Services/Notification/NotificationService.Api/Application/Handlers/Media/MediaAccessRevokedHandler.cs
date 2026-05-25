using System.Globalization;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;

namespace Urfu.Link.Services.Notification.Application.Handlers.Media;

public sealed class MediaAccessRevokedHandler : INotificationHandler<MediaAccessRevokedEvent>
{
    public Task<IReadOnlyList<NotificationIntent>> PrepareAsync(
        MediaAccessRevokedEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _ = cancellationToken;

        var content = NotificationContent.Create(
            "Доступ к файлу закрыт",
            "Доступ к медиафайлу был отозван",
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
                Category: NotificationCategory.MediaAccessRevoked,
                Severity: NotificationSeverity.Normal,
                Content: content,
                Data: data,
                GroupKey: null,
                SourceEventId: integrationEvent.EventId,
                SourceEventType: integrationEvent.EventType,
                SourceActionId: NotificationSourceActions.MediaAsset(integrationEvent.AssetId, "access-revoked"),
                Priority: NotificationPriority.PinSystemAdmin))
            .ToArray();

        return Task.FromResult<IReadOnlyList<NotificationIntent>>(intents);
    }
}
