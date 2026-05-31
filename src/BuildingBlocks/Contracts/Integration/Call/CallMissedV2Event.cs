namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Call;

public sealed record CallMissedV2Event(
    Guid CallId,
    string ConversationId,
    Guid CallerId,
    Guid RecipientId,
    IReadOnlyList<Guid> ParticipantIds,
    CallType CallType,
    TimeSpan RingDuration,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "call.missed.v2";
}
