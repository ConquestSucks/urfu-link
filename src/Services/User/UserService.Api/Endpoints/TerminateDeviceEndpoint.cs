using FastEndpoints;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class TerminateDeviceRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public sealed class TerminateDeviceEndpoint(
    ISessionManager sessionManager,
    ISessionRevocationStore revocationStore)
    : Endpoint<TerminateDeviceRequest>
{
    public override void Configure()
    {
        Delete("/me/devices/{SessionId}");
        Summary(s => s.Summary = "Terminate a specific device session");
    }

    public override async Task HandleAsync(TerminateDeviceRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        await sessionManager.TerminateAsync(req.SessionId, ct).ConfigureAwait(false);

        var userId = HttpContext.User.GetUserId().ToString();
        var currentSessionId = HttpContext.User.GetSessionId();
        await revocationStore.RevokeAsync(userId, currentSessionId, ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
