using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Notification.Infrastructure.Outbox;

public interface IOutboxEnqueue
{
    void Enqueue<TEvent>(TEvent integrationEvent) where TEvent : IIntegrationEvent;
}
