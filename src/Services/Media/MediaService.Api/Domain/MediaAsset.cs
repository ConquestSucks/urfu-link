using MediaService.Api.Domain.Enums;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;
using IntegrationAssetKind = Urfu.Link.BuildingBlocks.Contracts.Integration.Media.MediaAssetKind;
using IntegrationVisibility = Urfu.Link.BuildingBlocks.Contracts.Integration.Media.MediaVisibility;

namespace MediaService.Api.Domain;

/// <summary>
/// Aggregate root for a single media asset stored in MinIO.
/// Lifecycle: Initiated (waiting for client PUT) → Uploaded (client confirmed)
/// → Deleted (soft) → HardDeleted (retention worker removed object).
/// All state transitions go through methods that record domain events; raw
/// setters are intentionally private to keep invariants enforced.
/// </summary>
public sealed class MediaAsset
{
    private readonly List<IIntegrationEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Visibility Visibility { get; private set; }
    public AssetKind Kind { get; private set; }
    public string Bucket { get; private set; } = string.Empty;
    public string ObjectKey { get; private set; } = string.Empty;
    public long Size { get; private set; }
    public string MimeType { get; private set; } = string.Empty;
    public string OriginalFileName { get; private set; } = string.Empty;
    public int? DurationSeconds { get; private set; }
    public string? Checksum { get; private set; }
    public AssetState State { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UploadedAtUtc { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public DateTimeOffset? HardDeletedAtUtc { get; private set; }

    public IReadOnlyList<IIntegrationEvent> DomainEvents => _domainEvents;

    private MediaAsset() { }

    /// <summary>
    /// Reserve a new asset record before the client uploads bytes to MinIO.
    /// The id is supplied by the caller so that the storage object key can
    /// reference it from the start.
    /// </summary>
    public static MediaAsset Initiate(
        Guid id,
        Guid ownerId,
        Visibility visibility,
        AssetKind kind,
        string bucket,
        string objectKey,
        long size,
        string mimeType,
        string originalFileName,
        int? durationSeconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (durationSeconds is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        return new MediaAsset
        {
            Id = id,
            OwnerId = ownerId,
            Visibility = visibility,
            Kind = kind,
            Bucket = bucket,
            ObjectKey = objectKey,
            Size = size,
            MimeType = mimeType,
            OriginalFileName = originalFileName,
            DurationSeconds = durationSeconds,
            State = AssetState.Initiated,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Confirm that the client successfully uploaded the bytes.
    /// Records <see cref="MediaAssetUploadedEvent"/>.
    /// </summary>
    public void MarkUploaded(string? checksum = null)
    {
        if (State != AssetState.Initiated)
            throw new InvalidOperationException($"Cannot mark uploaded: asset is in state {State}.");

        Checksum = checksum;
        State = AssetState.Uploaded;
        UploadedAtUtc = DateTimeOffset.UtcNow;
        _domainEvents.Add(new MediaAssetUploadedEvent(
            Id,
            OwnerId,
            (IntegrationVisibility)(int)Visibility,
            (IntegrationAssetKind)(int)Kind,
            Bucket,
            ObjectKey,
            Size,
            MimeType,
            OriginalFileName));
    }

    /// <summary>
    /// Mark a previously-initiated upload as failed (e.g. session expired).
    /// </summary>
    public void MarkFailed()
    {
        if (State != AssetState.Initiated)
            throw new InvalidOperationException($"Cannot mark failed: asset is in state {State}.");

        State = AssetState.Failed;
    }

    /// <summary>
    /// Soft-delete the asset; the object stays in MinIO until the retention worker
    /// performs hard delete after the configured TTL.
    /// </summary>
    public void SoftDelete()
    {
        if (State != AssetState.Uploaded)
            throw new InvalidOperationException($"Cannot soft-delete: asset is in state {State}.");

        State = AssetState.Deleted;
        DeletedAtUtc = DateTimeOffset.UtcNow;
        _domainEvents.Add(new MediaAssetDeletedEvent(Id, OwnerId, DeletedAtUtc.Value));
    }

    /// <summary>
    /// Permanently remove the asset (retention worker after TTL). Object key is
    /// kept for the event payload so consumers can purge any caches.
    /// </summary>
    public void HardDelete()
    {
        if (State != AssetState.Deleted)
            throw new InvalidOperationException($"Cannot hard-delete: asset is in state {State}.");

        State = AssetState.HardDeleted;
        HardDeletedAtUtc = DateTimeOffset.UtcNow;
        _domainEvents.Add(new MediaAssetHardDeletedEvent(Id, OwnerId, Bucket, ObjectKey));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// True when the asset is soft-deleted, hard-deleted or failed — i.e. download
    /// must be denied even if a grant exists.
    /// </summary>
    public bool IsAccessible => State == AssetState.Uploaded;
}
