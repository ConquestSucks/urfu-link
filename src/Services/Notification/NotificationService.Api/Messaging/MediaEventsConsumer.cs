using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;
using Urfu.Link.Services.Notification.Application.Handlers.Media;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed class MediaEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<MediaEventsConsumer> logger)
    : KafkaConsumerBase(scopeFactory, configuration, logger)
{
    protected override string Topic => KafkaTopicNames.MediaEvents;

    protected override string GroupId => "notification-service-media-v1";

    protected override string DedupKeyPrefix => "notif:media-events";

    protected override async Task HandleEventAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "media.access_granted.v1":
                {
                    var evt = payload.Deserialize<MediaAccessGrantedEvent>(JsonOptions)
                        ?? throw new JsonException("MediaAccessGrantedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<MediaAccessGrantedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "media.access_revoked.v1":
                {
                    var evt = payload.Deserialize<MediaAccessRevokedEvent>(JsonOptions)
                        ?? throw new JsonException("MediaAccessRevokedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<MediaAccessRevokedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "media.asset_uploaded.v1":
                {
                    var evt = payload.Deserialize<MediaAssetUploadedEvent>(JsonOptions)
                        ?? throw new JsonException("MediaAssetUploadedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<MediaAssetUploadedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "media.asset_deleted.v1":
                {
                    var evt = payload.Deserialize<MediaAssetDeletedEvent>(JsonOptions)
                        ?? throw new JsonException("MediaAssetDeletedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<MediaAssetDeletedHandler>(), evt, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "media.asset_hard_deleted.v1":
                break;

            default:
                break;
        }
    }
}
