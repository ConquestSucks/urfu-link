using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Call;
using Urfu.Link.Services.Notification.Application.Handlers.Call;
using Urfu.Link.Services.Notification.Application.Routing;
using Urfu.Link.Services.Notification.Application.Services;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed class CallEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<CallEventsConsumer> logger)
    : KafkaConsumerBase(scopeFactory, configuration, logger)
{
    protected override string Topic => KafkaTopicNames.CallEvents;

    protected override string GroupId => "notification-service-call-v1";

    protected override string DedupKeyPrefix => "notif:call-events";

    protected override async Task HandleEventAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "call.incoming.v1":
                {
                    var evt = payload.Deserialize<CallIncomingEvent>(JsonOptions)
                        ?? throw new JsonException("CallIncomingEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<CallIncomingHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "call.incoming.v2":
                {
                    var evt = payload.Deserialize<CallIncomingV2Event>(JsonOptions)
                        ?? throw new JsonException("CallIncomingV2Event payload null");
                    await RoutingDispatcher.Route(
                        scope,
                        scope.GetRequiredService<CallIncomingHandler>(),
                        evt,
                        cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "call.missed.v1":
                {
                    var evt = payload.Deserialize<CallMissedEvent>(JsonOptions)
                        ?? throw new JsonException("CallMissedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<CallMissedHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "call.missed.v2":
                {
                    var evt = payload.Deserialize<CallMissedV2Event>(JsonOptions)
                        ?? throw new JsonException("CallMissedV2Event payload null");
                    await RoutingDispatcher.Route(
                        scope,
                        scope.GetRequiredService<CallMissedHandler>(),
                        evt,
                        cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "call.ended.v1":
                {
                    var evt = payload.Deserialize<CallEndedEvent>(JsonOptions)
                        ?? throw new JsonException("CallEndedEvent payload null");
                    await scope.GetRequiredService<NotificationLifecycleService>()
                        .ArchiveBySourceActionAsync(
                            NotificationSourceActions.CallIncoming(evt.CallId),
                            evt.OccurredAtUtc,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            case "call.ended.v2":
                {
                    var evt = payload.Deserialize<CallEndedV2Event>(JsonOptions)
                        ?? throw new JsonException("CallEndedV2Event payload null");
                    await scope.GetRequiredService<NotificationLifecycleService>()
                        .ArchiveBySourceActionAsync(
                            NotificationSourceActions.CallIncoming(evt.CallId),
                            evt.OccurredAtUtc,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                }

            default:
                break;
        }
    }
}
