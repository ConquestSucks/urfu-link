namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatMessagePinnedEvent(
    string ConversationId,
    Guid MessageId,
    Guid PinnedByUserId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.message.pinned.v1";
}
