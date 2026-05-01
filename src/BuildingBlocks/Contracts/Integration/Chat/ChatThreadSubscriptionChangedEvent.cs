namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;

/// <summary>
/// A user's thread subscription was added, escalated, or removed. <c>Subscribed=true</c> covers
/// both the initial subscribe and reason escalation; <c>Subscribed=false</c> is emitted on
/// explicit unsubscribe.
/// </summary>
public sealed record ChatThreadSubscriptionChangedEvent(
    Guid RootMessageId,
    Guid UserId,
    bool Subscribed,
    ThreadSubscriptionReason Reason,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "chat.thread.subscription_changed.v1";
}
