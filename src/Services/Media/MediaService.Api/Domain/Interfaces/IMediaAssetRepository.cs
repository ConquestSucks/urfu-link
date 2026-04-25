namespace MediaService.Api.Domain.Interfaces;

public interface IMediaAssetRepository
{
    Task<MediaAsset?> GetByIdAsync(Guid assetId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MediaAsset>> GetByIdsAsync(IReadOnlyList<Guid> assetIds, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deleted assets older than <paramref name="cutoff"/> that are due for hard delete.
    /// </summary>
    Task<IReadOnlyList<MediaAsset>> GetForRetentionAsync(DateTimeOffset cutoff, int limit, CancellationToken cancellationToken);

    void Add(MediaAsset asset);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
