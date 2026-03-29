using FastEndpoints;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Endpoints;

public sealed class TerminateDeviceRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public sealed class TerminateDeviceEndpoint(ISessionManager sessionManager)
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
        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
