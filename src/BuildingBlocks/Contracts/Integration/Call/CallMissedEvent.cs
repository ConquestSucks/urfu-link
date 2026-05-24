namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Call;

public sealed record CallMissedEvent(
    Guid CallId,
    Guid CallerId,
    Guid RecipientId,
    CallType CallType,
    TimeSpan RingDuration,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "call.missed.v1";
}
