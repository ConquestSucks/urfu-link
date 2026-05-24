namespace Urfu.Link.BuildingBlocks.Contracts.Integration.System;

public sealed record SystemUpdateEvent(
    string UpdateId,
    string Version,
    string Title,
    string Body,
    IReadOnlyList<Guid> Recipients) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType => "system.update.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
