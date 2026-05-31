namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record UserUnenrolledEvent(
    Guid DisciplineId,
    Guid UserId,
    DisciplineRole? Role = null,
    Guid? SubgroupId = null) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.user_unenrolled.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
