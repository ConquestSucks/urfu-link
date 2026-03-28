using FastEndpoints;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class DeleteAvatarEndpoint(
    IUserRepository userRepository,
    IAvatarStorage avatarStorage) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/me/avatar");
        Summary(s => s.Summary = "Remove avatar");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);

        if (user is null)
        {
            await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
            return;
        }

        if (user.Account.AvatarUrl is not null)
        {
            await avatarStorage.DeleteAsync(user.Account.AvatarUrl, ct).ConfigureAwait(false);
        }

        user.RemoveAvatar();
        await userRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
