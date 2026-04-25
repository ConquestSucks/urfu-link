using MediaService.Api.Domain;
using MediaService.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaService.Api.Infrastructure.Persistence.Repositories;

public sealed class UploadSessionRepository(MediaDbContext dbContext) : IUploadSessionRepository
{
    public async Task<UploadSession?> GetByAssetIdAsync(Guid assetId, CancellationToken cancellationToken)
    {
        return await dbContext.UploadSessions
            .SingleOrDefaultAsync(s => s.AssetId == assetId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UploadSession>> GetExpiredAsync(
        DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        return await dbContext.UploadSessions
            .Where(s => !s.IsCompleted && s.ExpiresAtUtc < now)
            .OrderBy(s => s.ExpiresAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(UploadSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        dbContext.UploadSessions.Add(session);
    }

    public void Remove(UploadSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        dbContext.UploadSessions.Remove(session);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
