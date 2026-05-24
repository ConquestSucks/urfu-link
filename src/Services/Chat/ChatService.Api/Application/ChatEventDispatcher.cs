using System.Diagnostics;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Chat.Domain;

namespace Urfu.Link.Services.Chat.Application;

/// <summary>
/// Wraps each chat domain event in an integration envelope and enqueues it on the outbox so
/// the outbox publisher worker can deliver it to Kafka transactionally with respect to the
/// service's own state.
/// </summary>
public sealed class ChatEventDispatcher(IOutboxWriter outboxWriter, ServiceProfile descriptor)
{
    public Task PublishAsync<TEvent>(TEvent payload, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(payload);
        var envelope = new IntegrationEnvelope<TEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            Source: descriptor.ServiceName,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: payload);
        return outboxWriter.EnqueueAsync(KafkaTopicNames.ChatEvents, envelope, cancellationToken).AsTask();
    }
}
