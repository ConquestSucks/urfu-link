namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record DisciplineSubgroupArchivedEvent(
    Guid DisciplineId,
    Guid SubgroupId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.subgroup_archived.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
