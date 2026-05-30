using System.Collections.Concurrent;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;

namespace DisciplineChatE2ETests.Infrastructure;

public sealed record PublishedEvent(string Topic, string EventType, IIntegrationEvent Payload);

public sealed class FakeOutboxWriter : IOutboxWriter
{
    public ConcurrentBag<PublishedEvent> Published { get; } = [];

    public ValueTask EnqueueAsync<TEvent>(
        string topic,
        IntegrationEnvelope<TEvent> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Published.Add(new PublishedEvent(topic, envelope.Payload.EventType, envelope.Payload));
        return ValueTask.CompletedTask;
    }

    public void Clear() => Published.Clear();
}
