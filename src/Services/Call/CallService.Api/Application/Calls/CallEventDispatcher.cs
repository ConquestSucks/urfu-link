using System.Diagnostics;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Call.Domain;

namespace Urfu.Link.Services.Call.Application.Calls;

public sealed class CallEventDispatcher(IOutboxWriter outboxWriter, ServiceProfile descriptor)
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

        return outboxWriter.EnqueueAsync(KafkaTopicNames.CallEvents, envelope, cancellationToken).AsTask();
    }
}
