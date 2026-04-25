using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Chat.Domain.Events;

public sealed record ChatMessageUnpinnedEvent(
    string ConversationId,
    Guid MessageId,
    Guid UnpinnedByUserId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.message.unpinned.v1";
}
