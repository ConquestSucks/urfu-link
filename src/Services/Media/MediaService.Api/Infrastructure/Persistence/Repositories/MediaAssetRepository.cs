using System.Diagnostics;
using MediaService.Api.Domain;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;

namespace MediaService.Api.Infrastructure.Persistence.Repositories;

public sealed class MediaAssetRepository(
    MediaDbContext dbContext,
    IOutboxWriter outboxWriter,
    string serviceName) : IMediaAssetRepository
{
    public async Task<MediaAsset?> GetByIdAsync(Guid assetId, CancellationToken cancellationToken)
    {
        return await dbContext.Assets
            .FindAsync([assetId], cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetByIdsAsync(
        IReadOnlyList<Guid> assetIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assetIds);
        if (assetIds.Count == 0) return [];
        return await dbContext.Assets
            .Where(a => assetIds.Contains(a.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PagedAssets> ListByOwnerAsync(
        Guid ownerId, Guid? cursor, int limit, CancellationToken cancellationToken)
    {
        var query = dbContext.Assets.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.State == AssetState.Uploaded);

        if (cursor is { } cursorId)
        {
            var anchor = await dbContext.Assets.AsNoTracking()
                .Where(a => a.Id == cursorId)
                .Select(a => (DateTimeOffset?)a.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (anchor.HasValue)
            {
                query = query.Where(a => a.CreatedAtUtc < anchor.Value);
            }
        }

        var page = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = page.Count > limit;
        var items = hasMore ? page.GetRange(0, limit) : page;
        var nextCursor = hasMore ? items[^1].Id : (Guid?)null;
        return new PagedAssets(items, nextCursor);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetForRetentionAsync(
        DateTimeOffset cutoff, int limit, CancellationToken cancellationToken)
    {
        return await dbContext.Assets
            .Where(a => a.State == AssetState.Deleted && a.DeletedAtUtc != null && a.DeletedAtUtc < cutoff)
            .OrderBy(a => a.DeletedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(MediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        dbContext.Assets.Add(asset);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<MediaAsset>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var domainEvent in domainEvents)
        {
            await DispatchEventAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask DispatchEventAsync(IIntegrationEvent domainEvent, CancellationToken cancellationToken)
    {
        var envelope = new IntegrationEnvelope<IIntegrationEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            Source: serviceName,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: domainEvent);

        await outboxWriter.EnqueueAsync(KafkaTopicNames.MediaEvents, envelope, cancellationToken).ConfigureAwait(false);
    }
}
