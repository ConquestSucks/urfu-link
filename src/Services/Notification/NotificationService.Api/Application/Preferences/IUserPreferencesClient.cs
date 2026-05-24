namespace Urfu.Link.Services.Notification.Application.Preferences;

public interface IUserPreferencesClient
{
    Task<UserPreferences> GetAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserContact> GetContactAsync(Guid userId, CancellationToken cancellationToken);

    Task<UserPreferences> UpdateAsync(Guid userId, UserPreferences preferences, CancellationToken cancellationToken);

    void Invalidate(Guid userId);
}

public sealed record UserContact(string Email, string DisplayName, string Locale);
