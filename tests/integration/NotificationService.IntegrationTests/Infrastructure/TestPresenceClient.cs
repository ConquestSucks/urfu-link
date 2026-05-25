using Urfu.Link.Services.Notification.Application.Preferences;

namespace NotificationService.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only <see cref="IPresenceClient"/> whose answer is set per test.
/// </summary>
public sealed class TestPresenceClient : IPresenceClient
{
    public bool OnlineOnWeb { get; set; }

    public HashSet<string> ViewingContexts { get; } = new(StringComparer.Ordinal);

    public Task<bool> IsOnlineOnWebAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = userId;
        _ = cancellationToken;
        return Task.FromResult(OnlineOnWeb);
    }

    public Task<bool> IsViewingAsync(Guid userId, string contextKey, CancellationToken cancellationToken)
    {
        _ = userId;
        _ = cancellationToken;
        return Task.FromResult(ViewingContexts.Contains(contextKey));
    }
}
