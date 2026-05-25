namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Media;

public sealed record MediaAccessRevokedEvent(
    Guid AssetId,
    IReadOnlyList<Guid> UserIds,
    MediaGrantSource Source,
    string? SourceId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType { get; } = "media.access_revoked.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
