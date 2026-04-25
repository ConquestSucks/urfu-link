using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Chat.Domain.Events;

public sealed record ChatMessagePinnedEvent(
    string ConversationId,
    Guid MessageId,
    Guid PinnedByUserId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.message.pinned.v1";
}
