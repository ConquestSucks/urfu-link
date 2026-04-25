namespace MediaService.Api.Domain.Interfaces;

/// <summary>
/// Abstraction over the S3-compatible object store (MinIO in dev/prod).
/// Higher-level operations are split out from raw presigning so the storage
/// can be mocked / replaced in tests independently of URL generation.
/// </summary>
public interface IMediaObjectStorage
{
    Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieve metadata stored on the object after the client uploaded it.
    /// Used by /upload/complete to verify size/etag and confirm presence.
    /// </summary>
    Task<ObjectMetadata?> GetMetadataAsync(string bucket, string objectKey, CancellationToken cancellationToken);
}

public sealed record ObjectMetadata(long ContentLength, string? ETag, string? ContentType);
