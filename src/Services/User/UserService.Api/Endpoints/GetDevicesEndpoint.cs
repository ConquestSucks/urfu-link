using FastEndpoints;
using UserService.Api.Application.Contracts.Responses;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class GetDevicesEndpoint(ISessionManager sessionManager, IDeviceRegistry deviceRegistry)
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
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        var realIp = HttpContext.Request.Headers["X-Real-Ip"].FirstOrDefault()
                     ?? HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();

        var sessions = await sessionManager.GetSessionsAsync(userId, ct).ConfigureAwait(false);

        // The current session is the one whose IP matches the real client IP
        var currentSession = !string.IsNullOrEmpty(realIp)
            ? sessions.FirstOrDefault(s => string.Equals(s.IpAddress, realIp, StringComparison.Ordinal))
            : null;

        // Save device name for the current session on each request
        if (currentSession is not null && !string.IsNullOrEmpty(userAgent))
            await deviceRegistry.SaveAsync(currentSession.SessionId, userAgent, ct).ConfigureAwait(false);

        var deviceNames = await Task.WhenAll(
            sessions.Select(s => deviceRegistry.GetDeviceNameAsync(s.SessionId, ct))
        ).ConfigureAwait(false);

        var response = sessions.Select((s, i) => new DeviceSessionResponse(
            SessionId: s.SessionId,
            IpAddress: s.IpAddress,
            LastAccess: s.LastAccess,
            Browser: deviceNames[i],
            Os: s.Os,
            IsCurrent: currentSession is not null && string.Equals(s.SessionId, currentSession.SessionId, StringComparison.Ordinal)
        )).ToList();

        await HttpContext.Response.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
