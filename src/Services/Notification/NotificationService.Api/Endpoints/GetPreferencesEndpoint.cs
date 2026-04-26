using System.Globalization;
using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record PreferencesResponse(
    IReadOnlyDictionary<int, ChannelToggleResponse> Categories,
    QuietHoursResponse QuietHours,
    bool DndEnabled,
    string Locale,
    bool Sound);

public sealed record ChannelToggleResponse(bool Push, bool Email, bool InApp);

public sealed record QuietHoursResponse(string IanaTimezone, string? Start, string? End, bool Enabled);

public sealed class GetPreferencesEndpoint(IUserPreferencesClient client)
    : EndpointWithoutRequest<PreferencesResponse>
{
    public override void Configure()
    {
        Get("/me/notifications/preferences");
        Summary(s => s.Summary = "Read the caller's notification preferences (proxied from UserService)");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var prefs = await client.GetAsync(userId, ct).ConfigureAwait(false);

        var categories = prefs.Categories.ToDictionary(
            kv => (int)kv.Key,
            kv => new ChannelToggleResponse(kv.Value.Push, kv.Value.Email, kv.Value.InApp));

        var quietHours = new QuietHoursResponse(
            prefs.QuietHours.IanaTimezone,
            prefs.QuietHours.Enabled ? prefs.QuietHours.Start.ToString("HH:mm", CultureInfo.InvariantCulture) : null,
            prefs.QuietHours.Enabled ? prefs.QuietHours.End.ToString("HH:mm", CultureInfo.InvariantCulture) : null,
            prefs.QuietHours.Enabled);

        await Send.OkAsync(new PreferencesResponse(categories, quietHours, prefs.DndEnabled, prefs.Locale, prefs.Sound), ct)
            .ConfigureAwait(false);
    }
}
