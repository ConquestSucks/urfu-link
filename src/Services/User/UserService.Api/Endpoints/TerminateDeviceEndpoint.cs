using FastEndpoints;
using Urfu.Link.BuildingBlocks.SessionRevocation;
using UserService.Api.Domain.Interfaces;
using UserService.Api.Infrastructure.Auth;

namespace UserService.Api.Endpoints;

public sealed class TerminateDeviceEndpoint(
    ISessionManager sessionManager,
    IDeviceRegistry deviceRegistry,
    ISessionRevocationStore revocationStore,
    ILogger<TerminateDeviceEndpoint> logger)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/me/devices/{SessionId}");
        Summary(s => s.Summary = "Terminate a specific device session");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var keycloakSessionId = Route<string>("SessionId")!;

        // Resolve Pomerium sid BEFORE RemoveAsync (which deletes the mapping)
        var pomeriumSid = await deviceRegistry.GetPomeriumSidByKeycloakSessionAsync(keycloakSessionId, ct)
            .ConfigureAwait(false);

        await sessionManager.TerminateAsync(keycloakSessionId, ct).ConfigureAwait(false);
        await deviceRegistry.RemoveAsync(keycloakSessionId, ct).ConfigureAwait(false);

        var userId = HttpContext.User.GetUserId().ToString();

        if (pomeriumSid is not null)
        {
            await revocationStore.RevokeSingleAsync(userId, pomeriumSid, ct).ConfigureAwait(false);
        }
        else
        {
            logger.LogWarning(
                "No Pomerium mapping for Keycloak session {KeycloakSessionId}; "
                + "middleware revocation skipped — session terminated via Keycloak only",
                keycloakSessionId);
        }

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
