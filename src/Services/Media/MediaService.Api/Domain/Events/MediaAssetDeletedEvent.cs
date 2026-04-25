using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace MediaService.Api.Domain.Events;

/// <summary>
/// Published when an asset is soft-deleted (DELETE /media/{id}).
/// Object remains in MinIO for the retention period; HardDeletedEvent is
/// raised later when the retention worker physically removes it.
/// </summary>
public sealed record MediaAssetDeletedEvent(
    Guid AssetId,
    Guid OwnerId,
    DateTimeOffset DeletedAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType { get; } = "media.asset_deleted.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
