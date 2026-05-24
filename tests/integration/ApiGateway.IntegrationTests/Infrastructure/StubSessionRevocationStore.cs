using Urfu.Link.BuildingBlocks.SessionRevocation;

namespace ApiGateway.IntegrationTests.Infrastructure;

/// <summary>
/// In-memory <see cref="ISessionRevocationStore"/> stub: nothing is ever revoked.
/// Replaces the Redis-backed store in integration tests so the gateway can run without Redis.
/// </summary>
public sealed class StubSessionRevocationStore : ISessionRevocationStore
{
    public Task RevokeAsync(string userId, string callerSessionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RevokeSingleAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> IsRevokedAsync(string userId, string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
