namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

/// <summary>
/// A new reply was posted in a thread. NotificationService consumes this event to deliver
/// "new thread reply" notifications to the supplied <paramref name="Subscribers"/>; mentions
/// are surfaced separately via <see cref="ChatMentionCreatedEvent"/> for priority routing.
/// </summary>
public sealed record ChatThreadReplyPostedEvent(
    string ConversationId,
    Guid RootMessageId,
    Guid MessageId,
    Guid SenderId,
    IReadOnlyList<Guid> Subscribers,
    IReadOnlyList<Guid>? Mentions,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.thread.reply_posted.v1";
}
