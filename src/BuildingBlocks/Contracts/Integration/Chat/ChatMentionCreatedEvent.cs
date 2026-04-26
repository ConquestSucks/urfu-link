namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatMentionCreatedEvent(
    string ConversationId,
    Guid MessageId,
    Guid SenderId,
    IReadOnlyList<Guid> MentionedUserIds,
    DateTimeOffset OccurredAtUtc,
    Guid? ThreadRootId = null) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.mention.created.v1";
}
