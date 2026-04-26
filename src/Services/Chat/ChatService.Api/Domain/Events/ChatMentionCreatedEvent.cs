using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace Urfu.Link.Services.Chat.Domain.Events;

public sealed record ChatMentionCreatedEvent(
    string ConversationId,
    Guid MessageId,
    Guid SenderId,
    IReadOnlyList<Guid> MentionedUserIds,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.mention.created.v1";
}
