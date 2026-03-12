namespace Urfu.Link.BuildingBlocks.Contracts.Integration;

/// <summary>
/// Marker interface for domain events published to the integration bus.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }

    string EventType { get; }

    DateTimeOffset OccurredAtUtc { get; }
}
