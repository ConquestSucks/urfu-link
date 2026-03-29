using FastEndpoints;
using UserService.Api.Application.Contracts.Requests;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class UpdateAccountEndpoint(IUserRepository userRepository)
    : Endpoint<UpdateAccountRequest>
{
    public override void Configure()
    {
        Put("/me/account");
        Summary(s => s.Summary = "Update account settings (about me)");
    }

    public override async Task HandleAsync(UpdateAccountRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user is null)
        {
            user = UserProfile.CreateDefault(userId);
            user.UpdateAccount(req.AboutMe);
            userRepository.Add(user);
        }
        else
        {
            user.UpdateAccount(req.AboutMe);
        }

        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
