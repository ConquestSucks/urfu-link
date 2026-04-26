using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Urfu.Link.Services.Notification.Channels.PushChannel;
using Urfu.Link.Services.Notification.Domain.Enums;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;
using NotificationAggregate = Urfu.Link.Services.Notification.Domain.Aggregates.Notification;

namespace Urfu.Link.Services.Notification.Workers;

public abstract class DispatcherOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 64;

    public int MaxAttempts { get; set; } = 5;
}

public sealed class PushDispatcherOptions : DispatcherOptions
{
    public const string SectionName = "PushDispatcher";
}

public sealed class PushDispatcherWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PushDispatcherOptions> options,
    ILogger<PushDispatcherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DrainAsync(settings, stoppingToken).ConfigureAwait(false);
                if (processed == 0)
                {
                    await Task.Delay(settings.PollInterval, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PushDispatcherWorker failed");
                await Task.Delay(settings.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<int> DrainAsync(PushDispatcherOptions settings, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<PushDispatcher>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        await using var tx = await db.Database.BeginTransactionAsync(stoppingToken).ConfigureAwait(false);

        // FOR UPDATE SKIP LOCKED ensures multiple worker replicas don't pick the same delivery.
        var batch = await db.Deliveries
            .FromSqlInterpolated($@"
                SELECT * FROM notifications.deliveries
                WHERE channel = {(short)DeliveryChannel.Push}
                  AND status = {(short)DeliveryStatus.Pending}
                  AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {now})
                ORDER BY COALESCE(last_attempt_at_utc, '0001-01-01'::timestamptz)
                LIMIT {settings.BatchSize}
                FOR UPDATE SKIP LOCKED")
            .ToListAsync(stoppingToken)
            .ConfigureAwait(false);

        if (batch.Count == 0)
        {
            await tx.CommitAsync(stoppingToken).ConfigureAwait(false);
            return 0;
        }

        // Batch-load parent notifications to avoid N+1 queries.
        var notificationIds = batch.Select(d => d.NotificationId).Distinct().ToList();
        var notificationsById = await db.Notifications
            .Where(n => notificationIds.Contains(n.Id))
            .ToDictionaryAsync(n => n.Id, stoppingToken)
            .ConfigureAwait(false);

        foreach (var delivery in batch)
        {
            if (!notificationsById.TryGetValue(delivery.NotificationId, out var notification))
            {
                delivery.MarkSkipped(now, "notification_missing");
                continue;
            }

            await dispatcher.DispatchAsync(notification, delivery, stoppingToken).ConfigureAwait(false);

            if (delivery.Status == DeliveryStatus.Pending && delivery.Attempts >= settings.MaxAttempts)
            {
                delivery.MarkFinalFailed(now, "max_attempts_exceeded");
            }
        }

        await db.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
        await tx.CommitAsync(stoppingToken).ConfigureAwait(false);
        return batch.Count;
    }
}
