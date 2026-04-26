using System.Globalization;
using FastEndpoints;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.ValueObjects;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record UpdatePreferencesRequest(
    IReadOnlyDictionary<int, ChannelToggleResponse> Categories,
    QuietHoursResponse QuietHours,
    bool DndEnabled,
    string Locale,
    bool Sound);

public sealed class UpdatePreferencesEndpoint(IUserPreferencesClient client)
    : Endpoint<UpdatePreferencesRequest>
{
    public override void Configure()
    {
        Put("/me/notifications/preferences");
        Summary(s => s.Summary = "Replace the caller's notification preferences (proxied to UserService)");
    }

    public override async Task HandleAsync(UpdatePreferencesRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();

        var categories = req.Categories.ToDictionary(
            kv => (NotificationCategory)kv.Key,
            kv => new ChannelToggle(kv.Value.Push, kv.Value.Email, kv.Value.InApp));

        var quietHours = req.QuietHours.Enabled
            && TimeOnly.TryParseExact(req.QuietHours.Start ?? string.Empty, "HH:mm", out var start)
            && TimeOnly.TryParseExact(req.QuietHours.End ?? string.Empty, "HH:mm", out var end)
            ? QuietHours.Create(req.QuietHours.IanaTimezone, start, end)
            : QuietHours.Disabled(req.QuietHours.IanaTimezone);

        var preferences = new UserPreferences(
            categories,
            quietHours,
            req.DndEnabled,
            string.IsNullOrWhiteSpace(req.Locale) ? "ru-RU" : req.Locale,
            req.Sound);

        await client.UpdateAsync(userId, preferences, ct).ConfigureAwait(false);
        client.Invalidate(userId);

        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
