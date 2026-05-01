using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Domain.Interfaces;

public interface IPushDeviceRepository
{
    Task<PushDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PushDevice?> FindByUserAndTokenAsync(Guid userId, PushProvider provider, string token, CancellationToken cancellationToken);

    Task<IReadOnlyList<PushDevice>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Loads active devices with change tracking enabled — call when you intend to mutate them.
    /// </summary>
    Task<IReadOnlyList<PushDevice>> ListActiveByUserForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(PushDevice device, CancellationToken cancellationToken);

    Task RemoveAsync(PushDevice device, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
