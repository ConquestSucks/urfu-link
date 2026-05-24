namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatParticipantLeftEvent(
    string ConversationId,
    Guid UserId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.participant_left.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
