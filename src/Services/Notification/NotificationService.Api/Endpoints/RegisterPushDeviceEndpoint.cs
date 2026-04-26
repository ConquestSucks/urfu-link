using FastEndpoints;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Auth;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record RegisterPushDeviceRequest(
    PushProvider Provider,
    string Token,
    string DeviceFingerprint,
    string Platform,
    string? AppVersion,
    string? Locale);

public sealed class RegisterPushDeviceEndpoint(
    IPushDeviceRepository repository,
    TimeProvider timeProvider)
    : Endpoint<RegisterPushDeviceRequest, PushDeviceResponse>
{
    public override void Configure()
    {
        Post("/me/notifications/devices");
        Summary(s => s.Summary = "Register or refresh a push device for FCM/APNs delivery");
    }

    public override async Task HandleAsync(RegisterPushDeviceRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var userId = HttpContext.User.GetUserId();
        var now = timeProvider.GetUtcNow();

        var existing = await repository.FindByUserAndTokenAsync(userId, req.Provider, req.Token, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.Reactivate(req.Token, now);
            }
            else
            {
                existing.Touch(now);
            }

            await repository.SaveChangesAsync(ct).ConfigureAwait(false);
            await Send.OkAsync(PushDeviceResponse.FromDomain(existing), ct).ConfigureAwait(false);
            return;
        }

        var device = PushDevice.Register(
            userId,
            req.Provider,
            req.Token,
            req.DeviceFingerprint,
            req.Platform,
            req.AppVersion,
            req.Locale,
            now);

        await repository.AddAsync(device, ct).ConfigureAwait(false);
        await repository.SaveChangesAsync(ct).ConfigureAwait(false);

        await Send.OkAsync(PushDeviceResponse.FromDomain(device), ct).ConfigureAwait(false);
    }
}
