using FastEndpoints;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed class ListPushDevicesEndpoint(IPushDeviceRepository repository)
    : EndpointWithoutRequest<IReadOnlyList<PushDeviceResponse>>
{
    public override void Configure()
    {
        Get("/me/notifications/devices");
        Summary(s => s.Summary = "List the caller's registered push devices");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.GetUserId();
        var devices = await repository.ListActiveByUserAsync(userId, ct).ConfigureAwait(false);
        var dtos = devices.Select(PushDeviceResponse.FromDomain).ToList();
        await Send.OkAsync(dtos, ct).ConfigureAwait(false);
    }
}
