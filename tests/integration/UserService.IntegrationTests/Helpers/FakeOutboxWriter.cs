using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;

namespace UserService.IntegrationTests.Helpers;

public sealed class FakeOutboxWriter : IOutboxWriter
{
    public System.Collections.ObjectModel.Collection<object> PublishedEvents { get; } = [];

    public ValueTask EnqueueAsync<TEvent>(string topic, IntegrationEnvelope<TEvent> envelope, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        PublishedEvents.Add(envelope);
        return ValueTask.CompletedTask;
    }
}
