using Microsoft.Extensions.Logging;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Channels.EmailChannel;

public sealed class EmailChannel(
    ITemplateRenderer renderer,
    IEmailSender sender,
    TimeProvider timeProvider,
    ILogger<EmailChannel> logger)
{
    public async Task<EmailSendOutcome> DispatchAsync(
        NotificationAggregate notification,
        Delivery delivery,
        string locale,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        if (delivery.Channel != DeliveryChannel.Email)
        {
            throw new ArgumentException("Delivery is not email.", nameof(delivery));
        }

        var content = renderer.Render(
            notification.Category,
            locale,
            new EmailModel(
                notification.Content.Title,
                notification.Content.Body,
                notification.Content.DeepLink,
                notification.Content.ImageUrl,
                locale,
                notification.Data.Values));

        var result = await sender.SendAsync(new EmailEnvelope(delivery.Address, content), cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();

        switch (result.Outcome)
        {
            case EmailSendOutcome.Success:
                delivery.MarkSent(now);
                break;
            case EmailSendOutcome.Transient:
                delivery.RecordFailure(now, result.Error ?? "transient", ComputeBackoff(delivery.Attempts));
                break;
            case EmailSendOutcome.PermanentFailure:
                delivery.MarkFinalFailed(now, result.Error ?? "permanent_failure");
                break;
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Email dispatch outcome={Outcome} delivery={DeliveryId}", result.Outcome, delivery.Id);
        }

        return result.Outcome;
    }

    private static TimeSpan ComputeBackoff(int attempts) => attempts switch
    {
        <= 1 => TimeSpan.FromMinutes(5),
        2 => TimeSpan.FromMinutes(30),
        3 => TimeSpan.FromHours(2),
        _ => TimeSpan.FromHours(12),
    };
}
