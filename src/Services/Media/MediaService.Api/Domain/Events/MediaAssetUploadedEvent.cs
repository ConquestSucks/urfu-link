using MediaService.Api.Domain.Enums;
using Urfu.Link.BuildingBlocks.Contracts.Integration;

namespace MediaService.Api.Domain.Events;

/// <summary>
/// Published after the client confirms an upload via /media/upload/complete.
/// Consumed downstream (e.g. ChatService validates that an attachment really exists
/// before persisting a message that references it).
/// </summary>
public sealed record MediaAssetUploadedEvent(
    Guid AssetId,
    Guid OwnerId,
    Visibility Visibility,
    AssetKind Kind,
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
