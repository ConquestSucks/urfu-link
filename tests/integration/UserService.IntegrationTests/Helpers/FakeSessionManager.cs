using UserService.Api.Domain.Interfaces;

namespace UserService.IntegrationTests.Helpers;

public sealed class FakeSessionManager : ISessionManager
{
    private readonly List<DeviceSession> _sessions =
    [
        new("test-session-001", "127.0.0.1", DateTimeOffset.UtcNow, "Chrome", "Windows"),
        new("test-session-002", "192.168.1.1", DateTimeOffset.UtcNow.AddHours(-2), "Safari", "macOS"),
    ];

    public Task<IReadOnlyList<DeviceSession>> GetSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<DeviceSession>>(_sessions.ToList());
    }

    public Task TerminateAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.RemoveAll(s => s.SessionId == sessionId);
        return Task.CompletedTask;
    }

    public Task TerminateAllExceptAsync(Guid userId, string currentSessionId, CancellationToken cancellationToken = default)
    {
        _sessions.RemoveAll(s => s.SessionId != currentSessionId);
        return Task.CompletedTask;
    }
}
