namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Discipline;

public sealed record DisciplineUpdatedEvent(
    Guid DisciplineId,
    string Code,
    string Title,
    string? Description,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.updated.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
