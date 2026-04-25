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
        var ownerScope = dbContext.Assets.AsNoTracking()
            .Where(a => a.OwnerId == ownerId && a.State == AssetState.Uploaded);

        if (cursor is not { } cursorId)
        {
            var firstPage = await ownerScope
                .OrderByDescending(a => a.CreatedAtUtc)
                .ThenByDescending(a => a.Id)
                .Take(limit + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return BuildPage(firstPage, limit);
        }

        var anchor = await dbContext.Assets.AsNoTracking()
            .Where(a => a.Id == cursorId)
            .Select(a => new { a.CreatedAtUtc, a.Id })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (anchor is null)
        {
            // Unknown cursor: fall back to the first page to avoid surprising the caller.
            var firstPage = await ownerScope
                .OrderByDescending(a => a.CreatedAtUtc)
                .ThenByDescending(a => a.Id)
                .Take(limit + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return BuildPage(firstPage, limit);
        }

        // EF Core cannot translate Guid comparison operators to SQL, so the keyset
        // tie-breaker is applied in two steps: first the typically-tiny set of rows
        // sharing the anchor's CreatedAtUtc (filtered in memory by id ordinal), then
        // the bulk of strictly-older rows fetched via SQL with composite ordering.
        var siblings = await ownerScope
            .Where(a => a.CreatedAtUtc == anchor.CreatedAtUtc && a.Id != anchor.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var anchorComparer = Comparer<Guid>.Default;
        var sameTimeAfterAnchor = siblings
            .Where(a => anchorComparer.Compare(a.Id, anchor.Id) < 0)
            .OrderByDescending(a => a.Id, anchorComparer)
            .ToList();

        var strictlyOlder = await ownerScope
            .Where(a => a.CreatedAtUtc < anchor.CreatedAtUtc)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ThenByDescending(a => a.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var combined = sameTimeAfterAnchor.Concat(strictlyOlder).Take(limit + 1).ToList();
        return BuildPage(combined, limit);
    }

    private static PagedAssets BuildPage(List<MediaAsset> rows, int limit)
    {
        var hasMore = rows.Count > limit;
        var items = hasMore ? rows.GetRange(0, limit) : rows;
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
