using Microsoft.Extensions.Logging;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Notifications;
using Urfu.Link.Services.Notification.Application.Preferences;
using Urfu.Link.Services.Notification.Application.Services;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Domain.Interfaces;
using Urfu.Link.Services.Notification.Infrastructure.Outbox;

namespace Urfu.Link.Services.Notification.Application.Routing;

/// <summary>
/// Coordinates a single integration event into per-recipient notifications:
/// resolves preferences, applies filters, persists the notification idempotently,
/// and emits one pending Delivery row per channel for the dispatcher workers.
/// </summary>
public sealed class NotificationRouter(
    IUserPreferencesClient preferencesClient,
    IPresenceClient presenceClient,
    INotificationRepository repository,
    IPushDeviceRepository pushDevices,
    NotificationFactory factory,
    TimeProvider timeProvider,
    IBadgeStore badgeStore,
    InAppChannel inAppChannel,
    IOutboxEnqueue outboxEnqueue,
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
        var updated = 0;
        var skipped = 0;

        foreach (var preparedIntent in drafts)
        {
            if (!string.IsNullOrWhiteSpace(preparedIntent.SuppressWhenViewingContextKey))
            {
                var isViewing = await presenceClient
                    .IsViewingAsync(
                        preparedIntent.RecipientUserId,
                        preparedIntent.SuppressWhenViewingContextKey,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (isViewing)
                {
                    skipped++;
                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug(
                            "Skipping notification {SourceActionId} for recipient {RecipientId}; user is viewing {ContextKey}",
                            preparedIntent.SourceActionId,
                            preparedIntent.RecipientUserId,
                            preparedIntent.SuppressWhenViewingContextKey);
                    }

                    continue;
                }
            }

            var intent = await ResolveActorAsync(preparedIntent, cancellationToken).ConfigureAwait(false);

            var preferences = await preferencesClient.GetAsync(intent.RecipientUserId, cancellationToken).ConfigureAwait(false);

            var candidates = SeverityRouter.Select(intent.Severity);
            var channels = PreferenceFilter.Filter(candidates, intent.Category, intent.Severity, preferences, timeProvider.GetUtcNow());

            var notification = factory.FromIntent(intent);

            foreach (var channel in channels)
            {
                if (channel == DeliveryChannel.InApp)
                {
                    notification.AddDelivery(Delivery.PendingInApp(notification.Id, $"user:{intent.RecipientUserId:N}"));
                }
                else if (channel == DeliveryChannel.Email)
                {
                    var contact = await preferencesClient.GetContactAsync(intent.RecipientUserId, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(contact.Email))
                    {
                        notification.AddDelivery(Delivery.PendingEmail(notification.Id, contact.Email));
                    }
                }
                else if (channel == DeliveryChannel.Push)
                {
                    var onlineOnWeb = await presenceClient.IsOnlineOnWebAsync(intent.RecipientUserId, cancellationToken).ConfigureAwait(false);
                    if (PresenceAwareSkipPolicy.ShouldSkipPush(intent.Category, intent.Severity, onlineOnWeb))
                    {
                        if (logger.IsEnabled(LogLevel.Debug))
                        {
                            logger.LogDebug(
                                "Skipping push for {Category} to {RecipientId} — user is online on web",
                                intent.Category,
                                intent.RecipientUserId);
                        }

                        continue;
                    }

                    var devices = await pushDevices.ListActiveByUserAsync(intent.RecipientUserId, cancellationToken).ConfigureAwait(false);
                    foreach (var device in devices)
                    {
                        notification.AddDelivery(Delivery.PendingPush(notification.Id, device.Provider, device.Id, device.Token));
                    }
                }
            }

            var upsert = await repository.UpsertAsync(notification, cancellationToken).ConfigureAwait(false);
            if (upsert.Status == NotificationUpsertStatus.Skipped)
            {
                skipped++;
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Notification with source_action_id={SourceActionId}, source_event_id={SourceEventId} for recipient {RecipientId} skipped",
                        notification.SourceActionId,
                        intent.SourceEventId,
                        intent.RecipientUserId);
                }

                continue;
            }

            var routedNotification = upsert.Notification ?? notification;
            if (upsert.Status == NotificationUpsertStatus.Created)
            {
                // Outbox event: published downstream once SaveChanges commits.
                outboxEnqueue.Enqueue(new NotificationCreatedEvent(
                    routedNotification.Id,
                    intent.RecipientUserId,
                    (int)routedNotification.Category,
                    intent.SourceEventId,
                    intent.SourceEventType));

                await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await badgeStore.IncrementAsync(intent.RecipientUserId, routedNotification.Category, cancellationToken)
                    .ConfigureAwait(false);
                created++;

                if (channels.Contains(DeliveryChannel.InApp))
                {
                    await inAppChannel.DeliverAsync(routedNotification, cancellationToken).ConfigureAwait(false);
                }

                continue;
            }

            updated++;
            await RebuildBadgeAsync(intent.RecipientUserId, cancellationToken).ConfigureAwait(false);
            if (channels.Contains(DeliveryChannel.InApp))
            {
                await inAppChannel.UpsertAsync(routedNotification, cancellationToken).ConfigureAwait(false);
            }
        }

        return new RoutingOutcome(created, updated, skipped);
    }

    private async Task RebuildBadgeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var counts = await repository.CountBadgeAsync(userId, cancellationToken).ConfigureAwait(false);
        await badgeStore.SetSnapshotAsync(
            userId,
            new BadgeSnapshot(counts.TotalUnread, counts.PerCategory),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<NotificationIntent> ResolveActorAsync(
        NotificationIntent intent,
        CancellationToken cancellationToken)
    {
        var actor = intent.Actor;
        if (actor?.Id is not { } actorId || actorId == Guid.Empty)
        {
            return intent;
        }

        if (!string.IsNullOrWhiteSpace(actor.DisplayName))
        {
            return intent;
        }

        var contact = await preferencesClient.GetContactAsync(actorId, cancellationToken)
            .ConfigureAwait(false);
        var displayName = string.IsNullOrWhiteSpace(contact.DisplayName)
            ? $"Пользователь {actorId.ToString("N")[..8]}"
            : contact.DisplayName.Trim();

        return intent with
        {
            Actor = actor with { DisplayName = displayName },
        };
    }
}

public sealed record RoutingOutcome(int Created, int Updated, int Skipped)
{
    public static RoutingOutcome NoDrafts { get; } = new(0, 0, 0);
}
