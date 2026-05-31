namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record DisciplineSubgroupCreatedEvent(
    Guid DisciplineId,
    Guid SubgroupId,
    string DisciplineTitle,
    Guid? DisciplineCoverAssetId,
    string Name,
    IReadOnlyList<Guid> TeacherUserIds,
    IReadOnlyList<Guid> StudentUserIds) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.subgroup_created.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
