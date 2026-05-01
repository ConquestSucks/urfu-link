using System.Diagnostics;
using System.Text.Json;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace Urfu.Link.Services.Notification.Infrastructure.Outbox;

/// <summary>
/// Writes outgoing integration events into <c>notifications.outbox_messages</c> in the
/// same EF transaction as the domain change. The relay worker publishes them to Kafka.
/// </summary>
public sealed class EfOutboxEnqueue(NotificationDbContext db, ServiceProfile descriptor) : IOutboxEnqueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Enqueue<TEvent>(TEvent integrationEvent) where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var envelope = new IntegrationEnvelope<TEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            Source: descriptor.ServiceName,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: integrationEvent);

        var payload = JsonSerializer.Serialize(envelope, JsonOptions);
        var message = OutboxMessage.Enqueue(
            descriptor.TopicName,
            integrationEvent.EventType,
            payload,
            DateTimeOffset.UtcNow);

        db.OutboxMessages.Add(message);
    }
}
