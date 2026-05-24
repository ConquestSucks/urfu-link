using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core repository for notifications and their owned deliveries. Idempotent insert
/// is implemented by translating Postgres unique-violation 23505 on
/// <c>ux_notifications_idempotency</c> into a "duplicate, swallow" signal.
/// </summary>
public sealed class NotificationRepository(NotificationDbContext db) : INotificationRepository
{
    public async Task<bool> TryInsertAsync(NotificationAggregate notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

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

    public Task<NotificationAggregate?> GetByIdAsync(Guid notificationId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        return db.Notifications
            .AsTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == recipientUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationAggregate>> ListAsync(
        Guid recipientUserId,
        NotificationCategory? category,
        bool unreadOnly,
        DateTimeOffset? cursorCreatedAtUtc,
        Guid? cursorId,
        int limit,
        CancellationToken cancellationToken)
    {
        IQueryable<NotificationAggregate> query = db.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == recipientUserId);

        if (category.HasValue)
        {
            query = query.Where(n => n.Category == category.Value);
        }

        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

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

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
