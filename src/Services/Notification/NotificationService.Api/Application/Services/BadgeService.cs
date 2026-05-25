using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Realtime;

namespace Urfu.Link.Services.Notification.Application.Services;

public sealed class BadgeService
{
    private readonly IBadgeStore _store;
    private readonly INotificationRepository? _repository;

    public BadgeService(IBadgeStore store)
    {
        _store = store;
    }

    public BadgeService(IBadgeStore store, INotificationRepository repository)
    {
        _store = store;
        _repository = repository;
    }

    public async Task<BadgeSnapshotDto> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await _store.GetSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        var perCategory = snapshot.PerCategory.ToDictionary(kv => (int)kv.Key, kv => kv.Value);

        if (_repository is null)
        {
            return new BadgeSnapshotDto(snapshot.Total, perCategory);
        }

        var counts = await _repository.CountBadgeAsync(userId, cancellationToken).ConfigureAwait(false);
        return new BadgeSnapshotDto(
            snapshot.Total,
            perCategory,
            counts.TotalUnseen,
            counts.UrgentUnread,
            counts.PerType);
    }
}
