namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

public sealed record ChatConversationArchivedEvent(
    string ConversationId,
    DateTimeOffset ArchivedAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.conversation_archived.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
