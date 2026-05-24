using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Realtime;

namespace Urfu.Link.Services.Notification.Application.Services;

public sealed class BadgeService(IBadgeStore store)
{
    public async Task<BadgeSnapshotDto> GetSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await store.GetSnapshotAsync(userId, cancellationToken).ConfigureAwait(false);
        var perCategory = snapshot.PerCategory.ToDictionary(kv => (int)kv.Key, kv => kv.Value);
        return new BadgeSnapshotDto(snapshot.Total, perCategory);
    }
}
