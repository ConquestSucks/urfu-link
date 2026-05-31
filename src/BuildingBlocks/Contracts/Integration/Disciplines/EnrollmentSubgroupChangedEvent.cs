namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record EnrollmentSubgroupChangedEvent(
    Guid DisciplineId,
    Guid UserId,
    Guid? OldSubgroupId,
    Guid? NewSubgroupId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.enrollment_subgroup_changed.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
