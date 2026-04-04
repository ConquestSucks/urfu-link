using System.Text;
using System.Text.Json;
using FastEndpoints;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class TerminateAllDevicesEndpoint(
    ISessionManager sessionManager,
    IDeviceRegistry deviceRegistry,
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
        var sessions = await sessionManager.GetSessionsAsync(userId, ct).ConfigureAwait(false);

        var currentKeycloakSessionId = await ResolveCurrentKeycloakSessionIdAsync(sessions, ct).ConfigureAwait(false);

        // If we can't identify the current session, do nothing to avoid self-logout
        if (currentKeycloakSessionId is null)
        {
            await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
            return;
        }

        var toTerminate = sessions
            .Where(s => !string.Equals(s.SessionId, currentKeycloakSessionId, StringComparison.Ordinal))
            .ToList();

        await Task.WhenAll(
            toTerminate.Select(s => sessionManager.TerminateAsync(s.SessionId, ct))
        ).ConfigureAwait(false);

        await deviceRegistry.RemoveAllAsync(toTerminate.Select(s => s.SessionId), ct).ConfigureAwait(false);

        var callerSessionId = HttpContext.User.GetSessionId();
        await revocationStore.RevokeAsync(userId.ToString(), callerSessionId, ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }

    private async Task<string?> ResolveCurrentKeycloakSessionIdAsync(
        IReadOnlyList<DeviceSession> sessions,
        CancellationToken ct)
    {
        var pomSid = GetPomeriumSid(
            HttpContext.Request.Headers["X-Pomerium-Jwt-Assertion"].FirstOrDefault());

        if (pomSid is not null)
        {
            var mapped = await deviceRegistry.GetKeycloakSessionIdAsync(pomSid, ct).ConfigureAwait(false);
            if (mapped is not null)
                return mapped;
        }

        var realIp = HttpContext.Request.Headers["X-Real-Ip"].FirstOrDefault()
                     ?? HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();

        return !string.IsNullOrEmpty(realIp)
            ? sessions.FirstOrDefault(s => string.Equals(s.IpAddress, realIp, StringComparison.Ordinal))?.SessionId
            : null;
    }

    private static string? GetPomeriumSid(string? jwtAssertion)
    {
        if (string.IsNullOrEmpty(jwtAssertion))
            return null;

        var parts = jwtAssertion.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = parts[1];
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/')));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sid", out var sid) ? sid.GetString() : null;
        }
        catch (FormatException) { return null; }
        catch (JsonException) { return null; }
    }
}
