namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Call;

public sealed record CallEndedEvent(
    Guid CallId,
    TimeSpan Duration,
    CallEndReason Reason,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "call.ended.v1";
}
