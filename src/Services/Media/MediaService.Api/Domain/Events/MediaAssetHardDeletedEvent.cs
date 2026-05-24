using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace MediaService.Api.Domain.Events;

/// <summary>
/// Published when the retention worker removes the underlying MinIO object and
/// transitions the asset to HardDeleted state. Final terminal event for the asset.
/// </summary>
public sealed record MediaAssetHardDeletedEvent(
    Guid AssetId,
    Guid OwnerId,
    string Bucket,
    string ObjectKey) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType { get; } = "media.asset_hard_deleted.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
