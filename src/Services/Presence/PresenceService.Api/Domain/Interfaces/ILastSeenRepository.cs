using Urfu.Link.Services.Presence.Domain.Aggregates;

namespace Urfu.Link.Services.Presence.Domain.Interfaces;

public interface ILastSeenRepository
{
    Task<LastSeen?> GetAsync(Guid userId, CancellationToken cancellationToken);

    void Upsert(LastSeen lastSeen);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
