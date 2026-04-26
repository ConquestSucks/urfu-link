using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Notification.Application.Handlers.Chat;
using Urfu.Link.Services.Notification.Application.Routing;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed class ChatEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ChatEventsConsumer> logger)
    : KafkaConsumerBase(scopeFactory, configuration, logger)
{
    protected override string Topic => KafkaTopicNames.ChatEvents;

    protected override string GroupId => "notification-service-chat-v1";

    protected override string DedupKeyPrefix => "notif:chat-events";

    protected override async Task HandleEventAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "chat.message.sent.v1":
                {
                    var evt = payload.Deserialize<ChatMessageSentEvent>(JsonOptions)
                        ?? throw new JsonException("ChatMessageSentEvent payload null");
                    var router = scope.GetRequiredService<NotificationRouter>();
                    var handler = scope.GetRequiredService<ChatMessageSentHandler>();
                    await router.RouteAsync(evt, handler, cancellationToken).ConfigureAwait(false);
                    break;
                }

            default:
                // Other chat events are routed in Wave 8.
                break;
        }
    }
}
