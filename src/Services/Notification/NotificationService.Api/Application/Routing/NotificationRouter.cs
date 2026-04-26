using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;

namespace Urfu.Link.Services.Notification.Application.Routing;

/// <summary>
/// Coordinates a single integration event into per-recipient notifications:
/// resolves preferences, applies filters, persists the notification idempotently,
/// and emits one pending Delivery row per channel for the dispatcher workers.
/// </summary>
public sealed class NotificationRouter(
    IUserPreferencesClient preferencesClient,
    INotificationRepository repository,
    IPushDeviceRepository pushDevices,
    NotificationFactory factory,
    TimeProvider timeProvider,
    IBadgeStore badgeStore,
    InAppChannel inAppChannel,
    ILogger<NotificationRouter> logger)
{
    public async Task<RoutingOutcome> RouteAsync<TEvent>(
        TEvent integrationEvent,
        INotificationHandler<TEvent> handler,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(handler);

        var drafts = await handler.PrepareAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
        if (drafts.Count == 0)
        {
            return RoutingOutcome.NoDrafts;
        }

        var created = 0;
        var skipped = 0;

        foreach (var draft in drafts)
        {
            var preferences = await preferencesClient.GetAsync(draft.RecipientUserId, cancellationToken).ConfigureAwait(false);

            var candidates = SeverityRouter.Select(draft.Severity);
            var channels = PreferenceFilter.Filter(candidates, draft.Category, draft.Severity, preferences, timeProvider.GetUtcNow());

            var notification = factory.FromDraft(draft);

            foreach (var channel in channels)
            {
                if (channel == DeliveryChannel.InApp)
                {
                    notification.AddDelivery(Delivery.PendingInApp(notification.Id, $"user:{draft.RecipientUserId:N}"));
                }
                else if (channel == DeliveryChannel.Email)
                {
                    var contact = await preferencesClient.GetContactAsync(draft.RecipientUserId, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(contact.Email))
                    {
                        notification.AddDelivery(Delivery.PendingEmail(notification.Id, contact.Email));
                    }
                }
                else if (channel == DeliveryChannel.Push)
                {
                    var devices = await pushDevices.ListActiveByUserAsync(draft.RecipientUserId, cancellationToken).ConfigureAwait(false);
                    foreach (var device in devices)
                    {
                        notification.AddDelivery(Delivery.PendingPush(notification.Id, device.Provider, device.Id, device.Token));
                    }
                }
            }

            var inserted = await repository.TryInsertAsync(notification, cancellationToken).ConfigureAwait(false);
            if (!inserted)
            {
                skipped++;
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Notification with source_event_id={SourceEventId} for recipient {RecipientId} already exists — skipped",
                        draft.SourceEventId,
                        draft.RecipientUserId);
                }

                continue;
            }

            await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await badgeStore.IncrementAsync(draft.RecipientUserId, draft.Category, cancellationToken).ConfigureAwait(false);
            created++;

            if (channels.Contains(DeliveryChannel.InApp))
            {
                await inAppChannel.DeliverAsync(notification, cancellationToken).ConfigureAwait(false);
            }
        }

        return new RoutingOutcome(created, skipped);
    }
}

public sealed record RoutingOutcome(int Created, int Skipped)
{
    public static RoutingOutcome NoDrafts { get; } = new(0, 0);
}
