using System.Collections.Concurrent;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Repositories;

/// <summary>
/// In-memory badge counter used until the Redis-backed implementation is wired up
/// (Wave 6). Safe for development and unit testing; production replaces this with
/// <c>RedisBadgeStore</c>.
/// </summary>
public sealed class InMemoryBadgeStore : IBadgeStore
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<NotificationCategory, int>> _values = new();

    public Task<int> IncrementAsync(Guid userId, NotificationCategory category, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var perCategory = _values.GetOrAdd(userId, _ => new ConcurrentDictionary<NotificationCategory, int>());
        perCategory.AddOrUpdate(category, 1, (_, current) => current + 1);
        var total = perCategory.Values.Sum();
        return Task.FromResult(total);
    }

    public Task<int> DecrementAsync(Guid userId, NotificationCategory category, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_values.TryGetValue(userId, out var perCategory))
        {
            perCategory.AddOrUpdate(category, 0, (_, current) => Math.Max(0, current - 1));
        }

        return Task.FromResult(perCategory?.Values.Sum() ?? 0);
    }

    public Task ResetAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _values.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    public Task<BadgeSnapshot> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_values.TryGetValue(userId, out var perCategory))
        {
            return Task.FromResult(BadgeSnapshot.Empty);
        }

        var snapshot = perCategory
            .Where(kv => kv.Value > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return Task.FromResult(new BadgeSnapshot(snapshot.Values.Sum(), snapshot));
    }

    public Task<int> SetSnapshotAsync(Guid userId, BadgeSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _ = cancellationToken;

        var perCategory = _values.GetOrAdd(userId, _ => new ConcurrentDictionary<NotificationCategory, int>());
        perCategory.Clear();
        foreach (var (category, count) in snapshot.PerCategory)
        {
            perCategory[category] = count;
        }

        return Task.FromResult(snapshot.Total);
    }
}
