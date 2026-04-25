namespace MediaService.Api.Domain;

/// <summary>
/// Tracks an in-flight upload from /media/upload/init until either /complete or expiration.
/// Used by <see cref="MediaService.Api.Domain"/> background workers to find orphaned objects
/// in MinIO that the client started uploading but never confirmed.
/// </summary>
public sealed class UploadSession
{
    public Guid Id { get; private set; }
    public Guid AssetId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public bool IsCompleted { get; private set; }

    private UploadSession() { }

    public static UploadSession Open(Guid assetId, TimeSpan ttl)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        var now = DateTimeOffset.UtcNow;
        return new UploadSession
        {
            Id = Guid.NewGuid(),
            AssetId = assetId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + ttl,
            IsCompleted = false,
        };
    }

    public void MarkCompleted()
    {
        if (IsCompleted) throw new InvalidOperationException("Upload session is already completed.");
        IsCompleted = true;
    }

    public bool IsExpired(DateTimeOffset now) => !IsCompleted && now >= ExpiresAtUtc;
}
