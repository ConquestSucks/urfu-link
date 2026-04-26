using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Domain.Events;

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
