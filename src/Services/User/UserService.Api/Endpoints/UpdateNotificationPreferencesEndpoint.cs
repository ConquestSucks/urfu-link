using System.Globalization;
using FastEndpoints;
using UserService.Api.Application.Contracts.Requests;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Domain.ValueObjects;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class UpdateNotificationPreferencesEndpoint(IUserRepository userRepository)
    : Endpoint<UpdateNotificationPreferencesRequest>
{
    public override void Configure()
    {
        Put("/me/notifications/preferences");
        Summary(s => s.Summary = "Replace per-category notification preferences and DND/quiet hours");
    }

    public override async Task HandleAsync(UpdateNotificationPreferencesRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();

        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        var settings = MapToSettings(req);

        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            user.UpdateNotificationPreferences(settings);
            userRepository.Add(user);
        }
        else
        {
            user.UpdateNotificationPreferences(settings);
        }

        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }

    private static NotificationSettings MapToSettings(UpdateNotificationPreferencesRequest request)
    {
        var categories = new Dictionary<int, ChannelToggle>();
        foreach (var (key, toggle) in request.Categories)
        {
            categories[key] = new ChannelToggle(toggle.Push, toggle.Email, toggle.InApp);
        }

        var quietHours = !request.QuietHours.Enabled
            ? QuietHours.Disabled(request.QuietHours.IanaTimezone)
            : QuietHours.Create(
                request.QuietHours.IanaTimezone,
                TimeOnly.ParseExact(request.QuietHours.Start!, "HH:mm", CultureInfo.InvariantCulture),
                TimeOnly.ParseExact(request.QuietHours.End!, "HH:mm", CultureInfo.InvariantCulture));

        return new NotificationSettings(
            categories,
            quietHours,
            request.DndEnabled,
            string.IsNullOrWhiteSpace(request.Locale) ? NotificationSettings.DefaultLocale : request.Locale,
            request.Sound);
    }
}
