namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Discipline;

public sealed record UserUnenrolledEvent(
    Guid DisciplineId,
    Guid UserId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.user_unenrolled.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
