using FastEndpoints;
using UserService.Api.Application.Contracts.Requests;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class UpdateNotificationsEndpoint(IUserRepository userRepository)
    : Endpoint<UpdateNotificationsRequest>
{
    public override void Configure()
    {
        Put("/me/notifications");
        Summary(s => s.Summary = "Update notification settings");
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
