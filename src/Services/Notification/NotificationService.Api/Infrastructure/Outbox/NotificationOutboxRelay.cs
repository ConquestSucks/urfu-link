using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace Urfu.Link.Services.Notification.Infrastructure.Outbox;

public sealed class NotificationOutboxOptions
{
    public const string SectionName = "NotificationOutbox";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 64;

    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(5);

    public int MaxAttempts { get; set; } = 8;
}

/// <summary>
/// Background relay that publishes pending <c>OutboxMessage</c> rows to Kafka. Failures
/// raise the <c>next_attempt_at_utc</c> with exponential backoff.
/// </summary>
public sealed class NotificationOutboxRelay(
    IServiceScopeFactory scopeFactory,
    IKafkaPublisher publisher,
    IOptions<NotificationOutboxOptions> options,
    ILogger<NotificationOutboxRelay> logger) : BackgroundService
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
                logger.LogError(ex, "OutboxRelay loop failed");
                await Task.Delay(settings.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<int> DrainAsync(NotificationOutboxOptions settings, CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

        await using var tx = await db.Database.BeginTransactionAsync(stoppingToken).ConfigureAwait(false);

        var batch = await db.OutboxMessages
            .FromSqlInterpolated($@"
                SELECT * FROM notifications.outbox_messages
                WHERE published_at_utc IS NULL AND next_attempt_at_utc <= {now}
                ORDER BY created_at_utc
                LIMIT {settings.BatchSize}
                FOR UPDATE SKIP LOCKED")
            .ToListAsync(stoppingToken)
            .ConfigureAwait(false);

        if (batch.Count == 0)
        {
            await tx.CommitAsync(stoppingToken).ConfigureAwait(false);
            return 0;
        }

        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishSerializedAsync(message.Topic, message.Id.ToString("N"), message.Payload, stoppingToken).ConfigureAwait(false);
                message.MarkPublished(now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var backoff = ComputeBackoff(message.Attempts, settings);
                message.RecordFailure(now, ex.Message, backoff);
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(ex, "OutboxMessage {Id} publish failed; backoff={Backoff}", message.Id, backoff);
                }
            }
        }

        await db.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
        await tx.CommitAsync(stoppingToken).ConfigureAwait(false);
        return batch.Count;
    }

    private static TimeSpan ComputeBackoff(int attempts, NotificationOutboxOptions settings)
    {
        var multiplier = Math.Pow(2, Math.Clamp(attempts, 0, 10));
        var seconds = settings.InitialBackoff.TotalSeconds * multiplier;
        var capped = Math.Min(seconds, settings.MaxBackoff.TotalSeconds);
        return TimeSpan.FromSeconds(capped);
    }
}
