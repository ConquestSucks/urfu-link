using FastEndpoints;
using UserService.Api.Application.Contracts.Requests;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class UpdatePrivacyEndpoint(IUserRepository userRepository)
    : Endpoint<UpdatePrivacyRequest>
{
    public override void Configure()
    {
        Put("/me/privacy");
        Summary(s => s.Summary = "Update privacy settings");
    }

    public override async Task HandleAsync(UpdatePrivacyRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            user.UpdatePrivacy(req.ShowOnlineStatus, req.ShowLastVisitTime);
            userRepository.Add(user);
        }
        else
        {
            user.UpdatePrivacy(req.ShowOnlineStatus, req.ShowLastVisitTime);
        }

        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
