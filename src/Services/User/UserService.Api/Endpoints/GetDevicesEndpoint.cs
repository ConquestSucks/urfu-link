using FastEndpoints;
using Microsoft.Extensions.Logging;
using UserService.Api.Application.Contracts.Responses;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class GetDevicesEndpoint(ISessionManager sessionManager, IDeviceRegistry deviceRegistry, ILogger<GetDevicesEndpoint> logger)
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
        var currentKeycloakSessionId = HttpContext.Request.Headers["X-Keycloak-Session"].FirstOrDefault();
        var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();
        var xForwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        logger.LogInformation("[GetDevices] X-Keycloak-Session={KcSession} UA={UA} X-Forwarded-For={XFF}",
            currentKeycloakSessionId ?? "(null)", userAgent, xForwardedFor ?? "(null)");

        // Persist device name for current session on each request
        if (!string.IsNullOrEmpty(currentKeycloakSessionId) && !string.IsNullOrEmpty(userAgent))
            await deviceRegistry.SaveAsync(currentKeycloakSessionId, userAgent, ct).ConfigureAwait(false);

        var sessions = await sessionManager.GetSessionsAsync(userId, ct).ConfigureAwait(false);

        var deviceNames = await Task.WhenAll(
            sessions.Select(s => deviceRegistry.GetDeviceNameAsync(s.SessionId, ct))
        ).ConfigureAwait(false);

        var response = sessions.Select((s, i) => new DeviceSessionResponse(
            SessionId: s.SessionId,
            IpAddress: s.IpAddress,
            LastAccess: s.LastAccess,
            Browser: deviceNames[i],
            Os: s.Os,
            IsCurrent: string.Equals(s.SessionId, currentKeycloakSessionId, StringComparison.Ordinal))).ToList();

        await HttpContext.Response.SendAsync(response, cancellation: ct).ConfigureAwait(false);
    }
}
