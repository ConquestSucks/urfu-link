namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Discipline;

public sealed record UserEnrolledEvent(
    Guid DisciplineId,
    Guid UserId,
    DisciplineRole Role,
    Guid EnrolledBy) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.user_enrolled.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
