namespace Urfu.Link.Services.Notification.Application.Preferences;

/// <summary>
/// Reports whether a user is currently active on a web/desktop SignalR connection.
/// Used to skip redundant Push notifications for chat categories when the user is
/// already receiving them in-app.
/// </summary>
public interface IPresenceClient
{
    Task<bool> IsOnlineOnWebAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> IsViewingAsync(Guid userId, string contextKey, CancellationToken cancellationToken);
}

/// <summary>
/// Default fallback that assumes everyone is offline — preserves the prior behavior
/// of always sending push. Replace with a gRPC PresenceService client when wiring up
/// real presence tracking.
/// </summary>
public sealed class OfflinePresenceClient : IPresenceClient
{
    public Task<bool> IsOnlineOnWebAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = userId;
        _ = cancellationToken;
        return Task.FromResult(false);
    }

    public Task<bool> IsViewingAsync(Guid userId, string contextKey, CancellationToken cancellationToken)
    {
        _ = userId;
        _ = contextKey;
        _ = cancellationToken;
        return Task.FromResult(false);
    }
}
