namespace MediaService.Api.Domain.Interfaces;

public interface IUploadSessionRepository
{
    Task<UploadSession?> GetByAssetIdAsync(Guid assetId, CancellationToken cancellationToken);

    /// <summary>
    /// Sessions still in non-completed state past their TTL.
    /// </summary>
    Task<IReadOnlyList<UploadSession>> GetExpiredAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken);

    void Add(UploadSession session);

    void Remove(UploadSession session);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
