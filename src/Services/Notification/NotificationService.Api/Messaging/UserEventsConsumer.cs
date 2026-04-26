using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.User;
using Urfu.Link.Services.Notification.Application.Handlers.Admin;
using Urfu.Link.Services.Notification.Application.Preferences;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed class UserEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<UserEventsConsumer> logger)
    : KafkaConsumerBase(scopeFactory, configuration, logger)
{
    protected override string Topic => KafkaTopicNames.UserEvents;

    protected override string GroupId => "notification-service-user-v1";

    protected override string DedupKeyPrefix => "notif:user-events";

    protected override async Task HandleEventAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "user.role_changed.v1":
                {
                    var evt = payload.Deserialize<UserRoleChangedEvent>(JsonOptions)
                        ?? throw new JsonException("UserRoleChangedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<AdminRoleChangedHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "user.notification_settings_changed.v1":
                {
                    var evt = payload.Deserialize<UserNotificationSettingsChangedEvent>(JsonOptions)
                        ?? throw new JsonException("UserNotificationSettingsChangedEvent payload null");
                    var prefsClient = scope.GetRequiredService<IUserPreferencesClient>();
                    prefsClient.Invalidate(evt.UserId);
                    _ = cancellationToken;
                    break;
                }

            case "user.deleted.v1":
                {
                    var evt = payload.Deserialize<UserDeletedEvent>(JsonOptions)
                        ?? throw new JsonException("UserDeletedEvent payload null");
                    var handler = scope.GetRequiredService<UserDeletedHandler>();
                    await handler.HandleAsync(evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            default:
                break;
        }
    }
}
