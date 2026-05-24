namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatParticipantJoinedEvent(
    string ConversationId,
    Guid UserId,
    ChatParticipantRole Role) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.participant_joined.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
