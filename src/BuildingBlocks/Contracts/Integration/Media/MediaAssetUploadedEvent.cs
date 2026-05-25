namespace Urfu.Link.BuildingBlocks.Contracts.Integration.Media;

public sealed record MediaAssetUploadedEvent(
    Guid AssetId,
    Guid OwnerId,
    MediaVisibility Visibility,
    MediaAssetKind Kind,
    string Bucket,
    string ObjectKey,
    long Size,
    string MimeType,
    string OriginalFileName) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();

    public string EventType { get; } = "media.asset_uploaded.v1";

    public DateTimeOffset OccurredAtUtc { get; } = DateTimeOffset.UtcNow;
}
