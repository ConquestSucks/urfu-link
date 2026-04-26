namespace Urfu.Link.Services.Notification.Application.Preferences;

/// <summary>
/// Returns <see cref="UserPreferences.Default"/> for every user. Acts as a placeholder
/// until the real gRPC client to UserService is wired in (Wave 13).
/// </summary>
public sealed class StubUserPreferencesClient : IUserPreferencesClient
{
    public Task<UserPreferences> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = userId;
        _ = cancellationToken;
        return Task.FromResult(UserPreferences.Default);
    }

    public Task<UserContact> GetContactAsync(Guid userId, CancellationToken cancellationToken)
    {
        _ = userId;
        _ = cancellationToken;
        return Task.FromResult(new UserContact(string.Empty, string.Empty, "ru-RU"));
    }

    public void Invalidate(Guid userId)
    {
        _ = userId;
    }
}
