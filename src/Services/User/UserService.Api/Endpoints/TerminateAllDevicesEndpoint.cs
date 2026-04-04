using FastEndpoints;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class TerminateAllDevicesEndpoint(
    ISessionManager sessionManager,
    ISessionRevocationStore revocationStore)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/me/devices");
        Summary(s => s.Summary = "Terminate all sessions except current");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var currentSessionId = HttpContext.User.GetSessionId();

        await sessionManager.TerminateAllExceptAsync(userId, currentSessionId, ct).ConfigureAwait(false);

        await revocationStore.RevokeAsync(userId.ToString(), currentSessionId, ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
