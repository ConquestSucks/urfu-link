namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatMessageDeletedEvent(
    string ConversationId,
    Guid MessageId,
    DeleteMode Mode,
    Guid DeletedBy,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.message.deleted.v1";
}
