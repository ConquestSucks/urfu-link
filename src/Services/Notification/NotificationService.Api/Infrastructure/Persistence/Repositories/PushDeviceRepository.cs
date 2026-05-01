using Microsoft.EntityFrameworkCore;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Repositories;

public sealed class PushDeviceRepository(NotificationDbContext db) : IPushDeviceRepository
{
    public Task<PushDevice?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => db.PushDevices.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<PushDevice?> FindByUserAndTokenAsync(Guid userId, PushProvider provider, string token, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return db.PushDevices
            .FirstOrDefaultAsync(
                d => d.UserId == userId && d.Provider == provider && d.Token == token,
                cancellationToken);
    }

    public async Task<IReadOnlyList<PushDevice>> ListActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.PushDevices
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PushDevice>> ListActiveByUserForUpdateAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.PushDevices
            .Where(d => d.UserId == userId && d.IsActive)
            .OrderBy(d => d.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task AddAsync(PushDevice device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        _ = cancellationToken;
        db.PushDevices.Add(device);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(PushDevice device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);
        _ = cancellationToken;
        db.PushDevices.Remove(device);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
