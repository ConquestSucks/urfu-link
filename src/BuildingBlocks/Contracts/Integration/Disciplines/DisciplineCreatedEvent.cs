namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record DisciplineCreatedEvent(
    Guid DisciplineId,
    string Code,
    string Title,
    string? Description,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.created.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
