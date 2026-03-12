using System.Diagnostics;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Presence.Domain;

namespace Urfu.Link.Services.Presence.Application;

public sealed record PublishSampleEventRequest(string Name);

public sealed record SampleIntegrationEvent(string Name) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType { get; } = "presence.sample.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}

public sealed class SampleEventDispatcher(
    ServiceProfile descriptor,
    IOutboxWriter outboxWriter)
{
    public async Task<Guid> PublishAsync(PublishSampleEventRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var integrationEvent = new SampleIntegrationEvent(request.Name);
        var envelope = new IntegrationEnvelope<SampleIntegrationEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            Source: descriptor.ServiceName,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: integrationEvent);

        await outboxWriter.EnqueueAsync(descriptor.TopicName, envelope, cancellationToken).ConfigureAwait(false);
        return envelope.MessageId;
    }
}

