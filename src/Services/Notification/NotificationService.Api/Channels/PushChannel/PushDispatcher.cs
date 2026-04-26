using Microsoft.Extensions.Logging;
using Urfu.Link.Services.Notification.Channels.PushChannel.Apns;
using Urfu.Link.Services.Notification.Channels.PushChannel.Fcm;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Channels.PushChannel;

/// <summary>
/// Translates a single push <see cref="Delivery"/> into a provider call (FCM or APNs)
/// and updates the delivery row according to the result. Token-invalid responses
/// deactivate the device so subsequent runs skip it.
/// </summary>
public sealed class PushDispatcher(
    IFcmClient fcmClient,
    IApnsClient apnsClient,
    IPushDeviceRepository pushDevices,
    TimeProvider timeProvider,
    ILogger<PushDispatcher> logger)
{
    public async Task<PushSendOutcome> DispatchAsync(
        NotificationAggregate notification,
        Delivery delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(delivery);

        if (delivery.Channel != DeliveryChannel.Push)
        {
            throw new ArgumentException("Delivery is not push.", nameof(delivery));
        }

        if (delivery.Provider is null || delivery.PushDeviceId is null)
        {
            delivery.MarkSkipped(timeProvider.GetUtcNow(), "missing_provider");
            return PushSendOutcome.PermanentFailure;
        }

        var payload = PushPayloadBuilder.For(notification, delivery);

        var result = delivery.Provider switch
        {
            PushProvider.Fcm => await fcmClient.SendAsync(payload, cancellationToken).ConfigureAwait(false),
            PushProvider.Apns => await apnsClient.SendAsync(payload, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown push provider {delivery.Provider}"),
        };

        var now = timeProvider.GetUtcNow();
        switch (result.Outcome)
        {
            case PushSendOutcome.Success:
                delivery.MarkSent(now, result.ProviderMessageId);
                break;

            case PushSendOutcome.TokenInvalid:
                delivery.MarkSkipped(now, $"token_invalid:{result.Error}");
                if (delivery.PushDeviceId is { } deviceId)
                {
                    var device = await pushDevices.GetByIdAsync(deviceId, cancellationToken).ConfigureAwait(false);
                    if (device is not null)
                    {
                        device.Deactivate(now, result.Error ?? "token_invalid");
                        await pushDevices.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                break;

            case PushSendOutcome.RetryLater:
                {
                    var backoff = ComputeBackoff(delivery.Attempts);
                    delivery.RecordFailure(now, result.Error ?? "retry_later", backoff);
                    break;
                }

            case PushSendOutcome.PermanentFailure:
                delivery.MarkFinalFailed(now, result.Error ?? "permanent_failure");
                break;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug(
                "Push dispatch outcome={Outcome} provider={Provider} delivery={DeliveryId}",
                result.Outcome,
                delivery.Provider,
                delivery.Id);
        }

        return result.Outcome;
    }

    private static TimeSpan ComputeBackoff(int attempts) => attempts switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(30),
        _ => TimeSpan.FromHours(4),
    };
}
