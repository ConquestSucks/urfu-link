namespace Urfu.Link.BuildingBlocks.Contracts.Integration.System;

public sealed record SystemMaintenanceEvent(
    Guid MaintenanceId,
    string Title,
    string Body,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc,
    bool AffectsCriticalPath,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "system.maintenance.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
