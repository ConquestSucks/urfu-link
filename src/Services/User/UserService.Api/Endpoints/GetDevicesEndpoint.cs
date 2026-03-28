using FastEndpoints;
using UserService.Api.Application.Contracts.Responses;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class GetDevicesEndpoint(ISessionManager sessionManager)
    : EndpointWithoutRequest<List<DeviceSessionResponse>>
{
    public override void Configure()
    {
        Get("/me/devices");
        Summary(s => s.Summary = "Get active sessions/devices");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var currentSessionId = HttpContext.User.GetSessionId();

        var sessions = await sessionManager.GetSessionsAsync(userId, ct).ConfigureAwait(false);

        var response = sessions.Select(s => new DeviceSessionResponse(
            SessionId: s.SessionId,
            IpAddress: s.IpAddress,
            LastAccess: s.LastAccess,
            Browser: s.Browser,
            Os: s.Os,
            IsCurrent: string.Equals(s.SessionId, currentSessionId, StringComparison.Ordinal))).ToList();

        await HttpContext.Response.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
