using System.Collections.Concurrent;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory IOutboxWriter; tests can inspect <see cref="Published"/> to verify
/// integration events were enqueued without spinning up Redis.
/// </summary>
public sealed class FakeOutboxWriter : IOutboxWriter
{
    public ConcurrentBag<(string Topic, string EventType)> Published { get; } = [];

    public ValueTask EnqueueAsync<TEvent>(
        string topic,
        IntegrationEnvelope<TEvent> envelope,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Published.Add((topic, envelope.Payload.EventType));
        return ValueTask.CompletedTask;
    }
}
