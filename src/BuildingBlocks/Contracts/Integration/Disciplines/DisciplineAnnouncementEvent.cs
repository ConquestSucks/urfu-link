namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

public sealed record DisciplineAnnouncementEvent(
    Guid DisciplineId,
    Guid AnnouncementId,
    Guid AuthorTeacherId,
    string Title,
    string Body,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "discipline.announcement.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
