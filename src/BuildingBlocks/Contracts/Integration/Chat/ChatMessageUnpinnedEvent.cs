namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatMessageUnpinnedEvent(
    string ConversationId,
    Guid MessageId,
    Guid UnpinnedByUserId,
    DateTimeOffset OccurredAtUtc,
    IReadOnlyList<Guid>? Recipients = null) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.message.unpinned.v1";
}
