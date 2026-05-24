using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace Urfu.Link.Services.Presence.Domain.Interfaces;

public interface IPresenceSessionStore
{
    /// <summary>
    /// Atomically registers the session in Redis and reports whether it was the
    /// first active session for the user (i.e. the caller should publish
    /// <c>UserCameOnline</c>).
    /// </summary>
    Task<bool> AddSessionAsync(PresenceSession session, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically removes the session and reports outcome:
    /// <c>Removed</c> = true if the session existed; <c>WasLast</c> = true if no
    /// other sessions remain for the user. Both must be true to publish
    /// <c>UserWentOffline</c> and avoid double events from sweeper races.
    /// </summary>
    Task<(bool Removed, bool WasLast)> RemoveSessionAsync(Guid userId, string deviceId, CancellationToken cancellationToken);

    Task RefreshHeartbeatAsync(Guid userId, string deviceId, DateTimeOffset utcNow, CancellationToken cancellationToken);

    Task UpdateSessionStatusAsync(
        Guid userId,
        string deviceId,
        PresenceStatus status,
        string? customActivity,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PresenceSession>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns up to <paramref name="limit"/> session keys whose last heartbeat
    /// happened before <paramref name="cutoffUtc"/>. Used by the sweeper.
    /// </summary>
    Task<IReadOnlyList<(Guid UserId, string DeviceId)>> GetExpiredSessionsAsync(
        DateTimeOffset cutoffUtc, int limit, CancellationToken cancellationToken);
}
