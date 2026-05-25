namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatReactionAddedEvent(
    string ConversationId,
    Guid MessageId,
    Guid UserId,
    string Emoji,
    DateTimeOffset OccurredAtUtc,
    Guid? MessageAuthorId = null) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.reaction.added.v1";
}
