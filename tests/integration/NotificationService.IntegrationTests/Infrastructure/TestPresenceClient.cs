using Urfu.Link.Services.Notification.Application.Preferences;

namespace NotificationService.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only <see cref="IPresenceClient"/> whose answer is set per test.
/// </summary>
public sealed class TestPresenceClient : IPresenceClient
{
    public bool OnlineOnWeb { get; set; }

    public Task<bool> IsOnlineOnWebAsync(Guid userId, CancellationToken cancellationToken)
    {
        return Task.FromResult(OnlineOnWeb);
    }
}
