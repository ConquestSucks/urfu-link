using FastEndpoints;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record RemovePushDeviceRequest(Guid DeviceId);

public sealed class RemovePushDeviceEndpoint(IPushDeviceRepository repository)
    : Endpoint<RemovePushDeviceRequest>
{
    public override void Configure()
    {
        Delete("/me/notifications/devices/{DeviceId:guid}");
        Summary(s => s.Summary = "Unregister a push device");
    }

    public override async Task HandleAsync(RemovePushDeviceRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();

        var device = await repository.GetByIdAsync(req.DeviceId, ct).ConfigureAwait(false);
        if (device is null || device.UserId != userId)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        await repository.RemoveAsync(device, ct).ConfigureAwait(false);
        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
