using FastEndpoints;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class TerminateAllDevicesEndpoint(ISessionManager sessionManager, IDeviceRegistry deviceRegistry)
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
        var realIp = HttpContext.Request.Headers["X-Real-Ip"].FirstOrDefault()
                     ?? HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();

        var sessions = await sessionManager.GetSessionsAsync(userId, ct).ConfigureAwait(false);
        var currentSessionId = !string.IsNullOrEmpty(realIp)
            ? sessions.FirstOrDefault(s => string.Equals(s.IpAddress, realIp, StringComparison.Ordinal))?.SessionId
            : null;

        var toTerminate = sessions
            .Where(s => !string.Equals(s.SessionId, currentSessionId, StringComparison.Ordinal))
            .ToList();

        await Task.WhenAll(
            toTerminate.Select(s => sessionManager.TerminateAsync(s.SessionId, ct))
        ).ConfigureAwait(false);

        await deviceRegistry.RemoveAllAsync(toTerminate.Select(s => s.SessionId), ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
