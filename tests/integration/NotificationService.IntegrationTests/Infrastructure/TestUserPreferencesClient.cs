using Urfu.Link.Services.Notification.Application.Preferences;

namespace NotificationService.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only <see cref="IUserPreferencesClient"/> that returns deterministic preferences
/// without booting a real UserService gRPC server. Tests can mutate
/// <see cref="DefaultPreferences"/> or supply per-user overrides via
/// <see cref="WithPreferences(System.Guid, UserPreferences)"/>.
/// </summary>
public sealed class TestUserPreferencesClient : IUserPreferencesClient
{
    private readonly Dictionary<Guid, UserPreferences> _overrides = new();
    private readonly Dictionary<Guid, UserContact> _contacts = new();

    public UserPreferences DefaultPreferences { get; set; } = UserPreferences.Default;

    public UserContact DefaultContact { get; set; } = new("user@test.local", "Test User", "ru-RU");

    public Task<UserPreferences> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_overrides.TryGetValue(userId, out var preferences) ? preferences : DefaultPreferences);

    public Task<UserContact> GetContactAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_contacts.TryGetValue(userId, out var contact) ? contact : DefaultContact);

    public Task<UserPreferences> UpdateAsync(Guid userId, UserPreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        _overrides[userId] = preferences;
        return Task.FromResult(preferences);
    }

    public void Invalidate(Guid userId) => _overrides.Remove(userId);

    public void WithPreferences(Guid userId, UserPreferences preferences) => _overrides[userId] = preferences;

    public void WithContact(Guid userId, UserContact contact) => _contacts[userId] = contact;
}
