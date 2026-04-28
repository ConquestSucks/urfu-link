namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatReactionRemovedEvent(
    string ConversationId,
    Guid MessageId,
    Guid UserId,
    string Emoji,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.reaction.removed.v1";
}
