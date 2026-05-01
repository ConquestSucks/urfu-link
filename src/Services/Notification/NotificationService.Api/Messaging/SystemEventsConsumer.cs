using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.System;
using Urfu.Link.Services.Notification.Application.Handlers.System;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed class SystemEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SystemEventsConsumer> logger)
    : KafkaConsumerBase(scopeFactory, configuration, logger)
{
    protected override string Topic => KafkaTopicNames.SystemEvents;

    protected override string GroupId => "notification-service-system-v1";

    protected override string DedupKeyPrefix => "notif:system-events";

    protected override async Task HandleEventAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "system.maintenance.v1":
                {
                    var evt = payload.Deserialize<SystemMaintenanceEvent>(JsonOptions)
                        ?? throw new JsonException("SystemMaintenanceEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<SystemMaintenanceHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "system.update.v1":
                {
                    var evt = payload.Deserialize<SystemUpdateEvent>(JsonOptions)
                        ?? throw new JsonException("SystemUpdateEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<SystemUpdateHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            default:
                break;
        }
    }
}
