namespace UserService.Api.Domain.Interfaces;

public interface ISessionManager
{
    Task<IReadOnlyList<DeviceSession>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task TerminateAsync(string sessionId, CancellationToken cancellationToken = default);
    Task TerminateAllExceptAsync(Guid userId, string currentSessionId, CancellationToken cancellationToken = default);
}

public sealed record DeviceSession(
    string SessionId,
    string? IpAddress,
    DateTimeOffset LastAccess,
    string? Browser,
    string? Os);
