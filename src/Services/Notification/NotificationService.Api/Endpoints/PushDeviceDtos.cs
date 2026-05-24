using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace Urfu.Link.Services.Notification.Endpoints;

public sealed record PushDeviceResponse(
    Guid Id,
    PushProvider Provider,
    string Token,
    string DeviceFingerprint,
    string Platform,
    string? AppVersion,
    string Locale,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    bool IsActive)
{
    public static PushDeviceResponse FromDomain(PushDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return new PushDeviceResponse(
            device.Id,
            device.Provider,
            device.Token,
            device.DeviceFingerprint,
            device.Platform,
            device.AppVersion,
            device.Locale,
            device.CreatedAtUtc,
            device.LastSeenAtUtc,
            device.IsActive);
    }
}
