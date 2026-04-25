using MediaService.Api.Domain;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaService.Api.Infrastructure.Persistence.Repositories;

public sealed class MediaAccessGrantRepository(MediaDbContext dbContext) : IMediaAccessGrantRepository
{
    public async Task<bool> HasAccessAsync(Guid assetId, Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Grants
            .AsNoTracking()
            .AnyAsync(g => g.AssetId == assetId && g.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MediaAccessGrant>> ListForAssetAsync(
        Guid assetId, CancellationToken cancellationToken)
    {
        return await dbContext.Grants
            .AsNoTracking()
            .Where(g => g.AssetId == assetId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddRangeAsync(IEnumerable<MediaAccessGrant> grants, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(grants);
        await dbContext.Grants.AddRangeAsync(grants, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> RemoveRangeAsync(
        Guid assetId,
        IReadOnlyList<Guid> userIds,
        GrantSource source,
        string? sourceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0) return 0;

        return await dbContext.Grants
            .Where(g =>
                g.AssetId == assetId &&
                userIds.Contains(g.UserId) &&
                g.Source == source &&
                g.SourceId == sourceId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> RemoveAllForSourceAsync(
        GrantSource source, string sourceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        return await dbContext.Grants
            .Where(g => g.Source == source && g.SourceId == sourceId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
