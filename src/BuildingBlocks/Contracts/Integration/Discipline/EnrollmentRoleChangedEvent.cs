namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Discipline;

public sealed record EnrollmentRoleChangedEvent(
    Guid DisciplineId,
    Guid UserId,
    DisciplineRole OldRole,
    DisciplineRole NewRole) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.enrollment_role_changed.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
