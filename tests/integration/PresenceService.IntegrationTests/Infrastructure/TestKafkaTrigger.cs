using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Presence.Messaging;

namespace PresenceService.IntegrationTests.Infrastructure;

/// <summary>
/// Helper that simulates a Kafka message arrival by invoking the registered
/// <see cref="IKafkaMessageHandler"/> directly. Lets integration tests cover
/// projection logic without spinning up Testcontainers Kafka.
/// </summary>
public static class TestKafkaTrigger
{
    public static async Task TriggerPrivacyChangedAsync(
        PresenceServiceFactory factory,
        Guid userId,
        bool showOnlineStatus,
        bool showLastVisitTime,
        CancellationToken cancellationToken = default)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IKafkaMessageHandler>().ToList();
        var payload = JsonSerializer.SerializeToElement(new
        {
            UserId = userId,
            ShowOnlineStatus = showOnlineStatus,
            ShowLastVisitTime = showLastVisitTime,
            EventId = Guid.NewGuid(),
            EventType = PrivacyChangedHandler.SubscribedEventType,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(PrivacyChangedHandler.SubscribedEventType, payload, cancellationToken);
        }
    }
}
