using MediaService.Api.Domain.Enums;

namespace MediaService.Api.Domain.Interfaces;

public interface IMediaAccessGrantRepository
{
    /// <summary>
    /// True when the user has at least one access grant on the asset (any source).
    /// </summary>
    Task<bool> HasAccessAsync(Guid assetId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MediaAccessGrant>> ListForAssetAsync(Guid assetId, CancellationToken cancellationToken);

    Task AddRangeAsync(IEnumerable<MediaAccessGrant> grants, CancellationToken cancellationToken);

    /// <summary>
    /// Remove grants matching (assetId, userId, source, sourceId).
    /// </summary>
    Task<int> RemoveRangeAsync(
        Guid assetId,
        IReadOnlyList<Guid> userIds,
        GrantSource source,
        string? sourceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Remove all grants attached to the given source (cascade when conversation
    /// or discipline is deleted).
    /// </summary>
    Task<int> RemoveAllForSourceAsync(GrantSource source, string sourceId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
