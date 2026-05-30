namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Call;

public sealed record CallIncomingV2Event(
    Guid CallId,
    string ConversationId,
    Guid CallerId,
    IReadOnlyList<Guid> ParticipantIds,
    CallType CallType,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "call.incoming.v2";
}
