using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Domain.Events;

public sealed record ChatConversationCreatedEvent(
    string ConversationId,
    ConversationType Type,
    IReadOnlyList<Guid> Participants,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.conversation.created.v1";
}
