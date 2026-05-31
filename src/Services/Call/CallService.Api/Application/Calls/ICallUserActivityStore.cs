namespace Urfu.Link.Services.Call.Application.Calls;

public interface ICallUserActivityStore
{
    Task<Guid?> GetActiveCallAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> TryReserveActiveCallAsync(
        Guid userId,
        Guid callId,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    Task ReleaseActiveCallAsync(Guid userId, Guid callId, CancellationToken cancellationToken);

    Task<string?> SetClientInstanceAsync(
        Guid callId,
        Guid userId,
        string clientInstanceId,
        TimeSpan ttl,
        CancellationToken cancellationToken);
}
