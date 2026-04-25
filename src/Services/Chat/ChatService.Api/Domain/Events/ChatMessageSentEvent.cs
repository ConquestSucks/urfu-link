using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Chat.Domain.Events;

public sealed record ChatMessageSentEvent(
    string ConversationId,
    Guid MessageId,
    Guid SenderId,
    IReadOnlyList<Guid> Recipients,
    string Preview,
    bool HasAttachments,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.message.sent.v1";
}
