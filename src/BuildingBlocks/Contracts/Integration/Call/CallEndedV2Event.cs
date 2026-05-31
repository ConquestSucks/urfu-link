namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Call;

public sealed record CallEndedV2Event(
    Guid CallId,
    string ConversationId,
    Guid CallerId,
    IReadOnlyList<Guid> ParticipantIds,
    CallType CallType,
    TimeSpan Duration,
    CallEndReason Reason,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "call.ended.v2";
}
