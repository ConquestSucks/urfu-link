namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Media;

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
