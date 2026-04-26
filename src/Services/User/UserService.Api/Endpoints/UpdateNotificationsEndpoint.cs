using FastEndpoints;
using UserService.Api.Application.Contracts.Requests;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

/// <summary>
/// Legacy endpoint that accepts the old four-boolean payload. New clients should use
/// <c>PUT /me/notifications/preferences</c> which exposes the per-category structure.
/// Kept around for app-store builds shipped before the per-category preferences rolled out.
/// </summary>
public sealed class UpdateNotificationsEndpoint(IUserRepository userRepository)
    : Endpoint<UpdateNotificationsRequest>
{
    public override void Configure()
    {
        Put("/me/notifications");
        Summary(s =>
        {
            s.Summary = "[Deprecated] Update notification settings (use /me/notifications/preferences)";
            s.Description = "Legacy endpoint preserved for older mobile clients. Maps the four-boolean payload " +
                "onto per-category preferences. Will be removed once the legacy clients are deprecated.";
        });
    }

    public override async Task HandleAsync(UpdateNotificationsRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            user.UpdateNotifications(req.NewMessages, req.NotificationSound, req.DisciplineChatMessages, req.Mentions);
            userRepository.Add(user);
        }
        else
        {
            user.UpdateNotifications(req.NewMessages, req.NotificationSound, req.DisciplineChatMessages, req.Mentions);
        }

        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
