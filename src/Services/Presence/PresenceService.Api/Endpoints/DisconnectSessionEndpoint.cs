using FastEndpoints;
using Urfu.Link.Services.Presence.Application.Sessions;
using Urfu.Link.Services.Presence.Infrastructure.Auth;

namespace Urfu.Link.Services.Presence.Endpoints;

public sealed class DisconnectSessionEndpoint(DisconnectPresenceSessionService service)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("sessions/{deviceId}/disconnect");
        Group<PresenceGroup>();
        Summary(s => s.Summary = "Disconnect the caller's presence session for the provided device id.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var deviceId = Route<string>("deviceId")!;
        var userId = User.GetUserId();
        await service.DisconnectAsync(userId, deviceId, ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
