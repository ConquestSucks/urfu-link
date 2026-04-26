namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Call;

public sealed record CallIncomingEvent(
    Guid CallId,
    Guid CallerId,
    IReadOnlyList<Guid> Recipients,
    CallType CallType,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "call.incoming.v1";
}
