namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

/// <summary>
/// Published when ChatService materialises the group conversation backing a discipline.
/// Notification, search, and analytics services subscribe to this to attach their own
/// projections to the conversation id.
/// </summary>
public sealed record ChatDisciplineConversationCreatedEvent(
    string ConversationId,
    Guid DisciplineId,
    Guid OwnerTeacherId,
    string? Title,
    Guid? CoverAssetId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.discipline_conversation_created.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
