namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatMessageEditedEvent(
    string ConversationId,
    Guid MessageId,
    Guid EditorUserId,
    string NewBody,
    IReadOnlyList<Guid> Mentions,
    DateTimeOffset EditedAtUtc,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.message.edited.v1";
}
