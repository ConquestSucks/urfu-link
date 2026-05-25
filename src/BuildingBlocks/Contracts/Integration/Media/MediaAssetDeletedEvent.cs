namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Media;

public sealed record MediaAssetDeletedEvent(
    Guid AssetId,
    Guid OwnerId,
    DateTimeOffset DeletedAtUtc) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType { get; } = "media.asset_deleted.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
