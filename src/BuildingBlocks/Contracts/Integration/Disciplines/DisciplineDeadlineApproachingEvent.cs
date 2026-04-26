namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record DisciplineDeadlineApproachingEvent(
    Guid DisciplineId,
    Guid AssignmentId,
    string AssignmentTitle,
    DateTimeOffset DueAtUtc,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.deadline.approaching.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
