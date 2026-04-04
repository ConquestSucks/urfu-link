using FastEndpoints;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class TerminateDeviceEndpoint(
    ISessionManager sessionManager,
    IDeviceRegistry deviceRegistry,
    ISessionRevocationStore revocationStore)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/me/devices/{SessionId}");
        Summary(s => s.Summary = "Terminate a specific device session");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessionId = Route<string>("SessionId")!;
        await sessionManager.TerminateAsync(sessionId, ct).ConfigureAwait(false);
        await deviceRegistry.RemoveAsync(sessionId, ct).ConfigureAwait(false);

        var userId = HttpContext.User.GetUserId().ToString();
        await revocationStore.RevokeSingleAsync(userId, sessionId, ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
