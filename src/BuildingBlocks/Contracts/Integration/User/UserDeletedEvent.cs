namespace Urfu.Link.BuildingBlocks.Contracts.Integration.User;

public sealed record UserDeletedEvent(Guid UserId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "user.deleted.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
