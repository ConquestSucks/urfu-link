namespace Urfu.Link.BuildingBlocks.Contracts.Integration;

/// <summary>
/// Envelope for messages exchanged through Kafka topics.
/// </summary>
public sealed record IntegrationEnvelope<TPayload>(
    Guid MessageId,
    string TraceId,
    string Source,
    DateTimeOffset CreatedAtUtc,
    TPayload Payload)
    where TPayload : IIntegrationEvent;
