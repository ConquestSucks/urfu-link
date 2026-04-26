using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Chat.Domain.Events;

public sealed record ChatReactionAddedEvent(
    string ConversationId,
    Guid MessageId,
    Guid UserId,
    string Emoji,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.reaction.added.v1";
}
