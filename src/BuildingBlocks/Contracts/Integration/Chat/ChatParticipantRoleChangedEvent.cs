namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatParticipantRoleChangedEvent(
    string ConversationId,
    Guid UserId,
    ChatParticipantRole OldRole,
    ChatParticipantRole NewRole) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.participant_role_changed.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
