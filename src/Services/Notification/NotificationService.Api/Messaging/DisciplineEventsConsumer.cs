using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;
using Urfu.Link.Services.Notification.Application.Handlers.Discipline;

namespace Urfu.Link.Services.Notification.Messaging;

public sealed class DisciplineEventsConsumer(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DisciplineEventsConsumer> logger)
    : KafkaConsumerBase(scopeFactory, configuration, logger)
{
    protected override string Topic => KafkaTopicNames.DisciplineEvents;

    protected override string GroupId => "notification-service-discipline-v1";

    protected override string DedupKeyPrefix => "notif:discipline-events";

    protected override async Task HandleEventAsync(
        string eventType,
        JsonNode payload,
        IServiceProvider scope,
        CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case "discipline.user_enrolled.v1":
                {
                    var evt = payload.Deserialize<UserEnrolledEvent>(JsonOptions)
                        ?? throw new JsonException("UserEnrolledEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<UserEnrolledHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "discipline.user_unenrolled.v1":
                {
                    var evt = payload.Deserialize<UserUnenrolledEvent>(JsonOptions)
                        ?? throw new JsonException("UserUnenrolledEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<UserUnenrolledHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "discipline.enrollment_role_changed.v1":
                {
                    var evt = payload.Deserialize<EnrollmentRoleChangedEvent>(JsonOptions)
                        ?? throw new JsonException("EnrollmentRoleChangedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<EnrollmentRoleChangedHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "discipline.announcement.v1":
                {
                    var evt = payload.Deserialize<DisciplineAnnouncementEvent>(JsonOptions)
                        ?? throw new JsonException("DisciplineAnnouncementEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<DisciplineAnnouncementHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "discipline.material.published.v1":
                {
                    var evt = payload.Deserialize<DisciplineMaterialPublishedEvent>(JsonOptions)
                        ?? throw new JsonException("DisciplineMaterialPublishedEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<DisciplineMaterialHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "discipline.deadline.approaching.v1":
                {
                    var evt = payload.Deserialize<DisciplineDeadlineApproachingEvent>(JsonOptions)
                        ?? throw new JsonException("DisciplineDeadlineApproachingEvent payload null");
                    await RoutingDispatcher.Route(scope, scope.GetRequiredService<DisciplineDeadlineHandler>(), evt, cancellationToken).ConfigureAwait(false);
                    break;
                }

            case "discipline.created.v1":
            case "discipline.updated.v1":
            case "discipline.deleted.v1":
                break;

            default:
                break;
        }
    }
}
