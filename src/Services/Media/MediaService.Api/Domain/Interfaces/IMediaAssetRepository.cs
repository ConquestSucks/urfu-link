namespace MediaService.Api.Domain.Interfaces;

public interface IMediaAssetRepository
{
    Task<MediaAsset?> GetByIdAsync(Guid assetId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MediaAsset>> GetByIdsAsync(IReadOnlyList<Guid> assetIds, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the next page of <paramref name="ownerId"/>'s uploaded assets,
    /// newest first. <paramref name="cursor"/> is the id of the last asset
    /// returned by the previous page; <c>null</c> for the first page.
    /// </summary>
    Task<PagedAssets> ListByOwnerAsync(
        Guid ownerId, Guid? cursor, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deleted assets older than <paramref name="cutoff"/> that are due for hard delete.
    /// </summary>
    Task<IReadOnlyList<MediaAsset>> GetForRetentionAsync(DateTimeOffset cutoff, int limit, CancellationToken cancellationToken);

    void Add(MediaAsset asset);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record PagedAssets(IReadOnlyList<MediaAsset> Items, Guid? NextCursor);
