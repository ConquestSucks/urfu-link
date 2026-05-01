namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record DisciplineMaterialPublishedEvent(
    Guid DisciplineId,
    Guid MaterialId,
    Guid AuthorTeacherId,
    string Title,
    string? Description,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.material.published.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
