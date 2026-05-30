namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record DisciplineSubgroupUpdatedEvent(
    Guid DisciplineId,
    Guid SubgroupId,
    string Name) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.subgroup_updated.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
