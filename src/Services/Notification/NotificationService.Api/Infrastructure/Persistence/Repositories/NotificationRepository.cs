using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for notifications and their owned deliveries. Idempotent writes
/// use event-level keys for exact Kafka duplicates and source-action keys for the
/// user-facing "one visible item per action" contract.
/// </summary>
public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task<bool> TryInsertAsync(NotificationAggregate notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        db.NotificationDedupKeys.Add(NotificationDedupKey.Create(
            notification.RecipientUserId,
            notification.SourceEventId,
            notification.Type,
            notification.Id,
            notification.CreatedAtUtc));
        db.Notifications.Add(notification);
        foreach (var delivery in notification.Deliveries)
        {
            db.Deliveries.Add(delivery);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<NotificationUpsertResult> UpsertAsync(
        NotificationAggregate notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (string.IsNullOrWhiteSpace(notification.SourceActionId))
        {
            return await TryInsertAsync(notification, cancellationToken).ConfigureAwait(false)
                ? NotificationUpsertResult.Created(notification)
                : NotificationUpsertResult.Skipped();
        }

        var exactDuplicate = await db.NotificationDedupKeys
            .AsNoTracking()
            .AnyAsync(k =>
                k.RecipientUserId == notification.RecipientUserId &&
                k.SourceEventId == notification.SourceEventId &&
                k.NotificationType == notification.Type,
                cancellationToken)
            .ConfigureAwait(false);
        if (exactDuplicate)
        {
            return NotificationUpsertResult.Skipped();
        }

        var actionKey = await db.NotificationSourceActionKeys
            .AsTracking()
            .FirstOrDefaultAsync(k =>
                k.RecipientUserId == notification.RecipientUserId &&
                k.SourceActionId == notification.SourceActionId,
                cancellationToken)
            .ConfigureAwait(false);

        NotificationAggregate? existing = null;
        if (actionKey is not null)
        {
            existing = await db.Notifications
                .AsTracking()
                .FirstOrDefaultAsync(n =>
                    n.Id == actionKey.NotificationId &&
                    n.CreatedAtUtc == actionKey.NotificationCreatedAtUtc,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (existing is null)
        {
            db.NotificationDedupKeys.Add(NotificationDedupKey.Create(
                notification.RecipientUserId,
                notification.SourceEventId,
                notification.Type,
                notification.Id,
                notification.CreatedAtUtc));
            db.NotificationSourceActionKeys.Add(NotificationSourceActionKey.Create(
                notification.RecipientUserId,
                notification.SourceActionId,
                notification.Id,
                notification.CreatedAtUtc,
                notification.Priority,
                notification.CreatedAtUtc));
            db.Notifications.Add(notification);
            foreach (var delivery in notification.Deliveries)
            {
                db.Deliveries.Add(delivery);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return NotificationUpsertResult.Created(notification);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                db.ChangeTracker.Clear();
                return NotificationUpsertResult.Skipped();
            }
        }

        actionKey ??= NotificationSourceActionKey.Create(
            notification.RecipientUserId,
            notification.SourceActionId,
            existing.Id,
            existing.CreatedAtUtc,
            existing.Priority,
            notification.CreatedAtUtc);

        db.NotificationDedupKeys.Add(NotificationDedupKey.Create(
            notification.RecipientUserId,
            notification.SourceEventId,
            notification.Type,
            existing.Id,
            notification.CreatedAtUtc));

        if (NotificationPriorityPolicy.ShouldUpgrade(existing.Priority, notification.Priority))
        {
            existing.ApplyIntent(
                notification.Category,
                notification.Severity,
                notification.Type,
                notification.Content,
                notification.Data,
                notification.Actor,
                notification.Entity,
                notification.Actions,
                notification.GroupKey,
                notification.Priority,
                notification.SourceEventId,
                notification.SourceEventType,
                notification.LastOccurrenceAtUtc);
            actionKey.PointTo(existing.Id, existing.CreatedAtUtc, existing.Priority);

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return NotificationUpsertResult.Updated(existing);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                db.ChangeTracker.Clear();
                return NotificationUpsertResult.Skipped();
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return NotificationUpsertResult.Skipped(existing);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            db.ChangeTracker.Clear();
            return NotificationUpsertResult.Skipped();
        }
    }

    public async Task<IReadOnlyList<NotificationAggregate>> ArchiveBySourceActionAsync(
        string sourceActionId,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceActionId);

        var notifications = await db.Notifications
            .AsTracking()
            .Where(n =>
                n.SourceActionId == sourceActionId &&
                n.DoneAtUtc == null &&
                n.ArchivedAtUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var notification in notifications)
        {
            notification.Archive(archivedAtUtc);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return notifications;
    }

    public Task<NotificationAggregate?> GetByIdAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        return db.Notifications
            .AsTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == recipientUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationAggregate>> ListAsync(
        Guid recipientUserId,
        NotificationListFilter filter,
        DateTimeOffset? cursorCreatedAtUtc,
        Guid? cursorId,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<NotificationAggregate> query = db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId);

        query = ApplyFilter(query, filter);

        if (cursorCreatedAtUtc.HasValue && cursorId.HasValue)
        {
            var cursorTs = cursorCreatedAtUtc.Value;
            var cursorIdValue = cursorId.Value;
            query = query.Where(n =>
                n.CreatedAtUtc < cursorTs ||
                (n.CreatedAtUtc == cursorTs && n.Id.CompareTo(cursorIdValue) < 0));
        }

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<NotificationAggregate>> ListForBulkAsync(
        Guid recipientUserId,
        NotificationListFilter filter,
        IReadOnlyList<Guid>? ids,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        IQueryable<NotificationAggregate> query = db.Notifications
            .AsTracking()
            .Where(n => n.RecipientUserId == recipientUserId);

        if (ids is { Count: > 0 })
        {
            query = query.Where(n => ids.Contains(n.Id));
        }
        else
        {
            query = ApplyFilter(query, filter);
        }

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> MarkAllAsReadAsync(
        Guid recipientUserId,
        NotificationCategory? category,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        IQueryable<NotificationAggregate> query = db.Notifications
            .Where(n => n.RecipientUserId == recipientUserId && n.ReadAtUtc == null);

        if (category.HasValue)
        {
            query = query.Where(n => n.Category == category.Value);
        }

        return await query
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAtUtc, readAtUtc), cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        return db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId && n.ReadAtUtc == null)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<NotificationCategory, int>> CountUnreadPerCategoryAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken)
    {
        var grouped = await db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId && n.ReadAtUtc == null)
            .GroupBy(n => n.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return grouped.ToDictionary(g => g.Key, g => g.Count);
    }

    public async Task<NotificationBadgeCounts> CountBadgeAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var unreadQuery = db.Notifications
            .AsNoTracking()
            .Where(n =>
                n.RecipientUserId == recipientUserId &&
                n.ReadAtUtc == null &&
                n.DoneAtUtc == null &&
                n.ArchivedAtUtc == null);

        var totalUnread = await unreadQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalUnseen = await unreadQuery.CountAsync(n => n.SeenAtUtc == null, cancellationToken).ConfigureAwait(false);
        var urgentUnread = await unreadQuery.CountAsync(n => n.Severity == NotificationSeverity.Urgent, cancellationToken)
            .ConfigureAwait(false);

        var perCategory = await unreadQuery
            .GroupBy(n => n.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var perType = await unreadQuery
            .GroupBy(n => n.Type)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new NotificationBadgeCounts(
            totalUnread,
            totalUnseen,
            urgentUnread,
            perCategory.ToDictionary(g => g.Key, g => g.Count),
            perType.ToDictionary(g => g.Key, g => g.Count, StringComparer.Ordinal));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<NotificationAggregate> ApplyFilter(
        IQueryable<NotificationAggregate> query,
        NotificationListFilter filter)
    {
        if (filter.Category.HasValue)
        {
            query = query.Where(n => n.Category == filter.Category.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            var type = filter.Type.Trim();
            query = query.Where(n => n.Type == type);
        }

        if (filter.Severity.HasValue)
        {
            query = query.Where(n => n.Severity == filter.Severity.Value);
        }

        if (filter.From.HasValue)
        {
            var from = filter.From.Value;
            query = query.Where(n => n.CreatedAtUtc >= from);
        }

        if (filter.To.HasValue)
        {
            var to = filter.To.Value;
            query = query.Where(n => n.CreatedAtUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var term = filter.Query.Trim();
            query = query.Where(n =>
                EF.Functions.ILike(n.Content.Title, $"%{term}%") ||
                EF.Functions.ILike(n.Content.Body, $"%{term}%"));
        }

        return NormalizeStatus(query, filter.Status);
    }

    private static IQueryable<NotificationAggregate> NormalizeStatus(
        IQueryable<NotificationAggregate> query,
        string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(n => n.DoneAtUtc == null && n.ArchivedAtUtc == null);
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "UNREAD" => query.Where(n => n.ReadAtUtc == null && n.DoneAtUtc == null && n.ArchivedAtUtc == null),
            "READ" => query.Where(n => n.ReadAtUtc != null && n.DoneAtUtc == null && n.ArchivedAtUtc == null),
            "SEEN" => query.Where(n => n.SeenAtUtc != null && n.DoneAtUtc == null && n.ArchivedAtUtc == null),
            "UNSEEN" => query.Where(n => n.SeenAtUtc == null && n.DoneAtUtc == null && n.ArchivedAtUtc == null),
            "SAVED" => query.Where(n => n.SavedAtUtc != null && n.ArchivedAtUtc == null),
            "DONE" => query.Where(n => n.DoneAtUtc != null),
            "ARCHIVED" => query.Where(n => n.ArchivedAtUtc != null),
            _ => query.Where(n => n.DoneAtUtc == null && n.ArchivedAtUtc == null),
        };
    }
}
