using Urfu.Link.Services.Call.Domain;

namespace Urfu.Link.Services.Call.Application.Calls;

public interface ICallSessionStore
{
    Task<bool> TryCreateAsync(CallSession session, TimeSpan ttl, CancellationToken cancellationToken);

    Task<CallSession?> GetAsync(Guid callId, CancellationToken cancellationToken);

    Task SaveAsync(CallSession session, TimeSpan ttl, CancellationToken cancellationToken);

    Task<bool> TrySaveAsync(
        CallSession expectedSession,
        CallSession session,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    Task RemoveFromExpiryIndexAsync(Guid callId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CallSession>> ListExpiredRingingAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken);
}
