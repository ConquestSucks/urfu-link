using Confluent.Kafka;
using DisciplineService.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Outbox;
using OutboxMessage = DisciplineService.Api.Domain.Aggregates.OutboxMessage;

namespace DisciplineService.Api.Infrastructure.Outbox;

public sealed class DisciplineOutboxRelayOptions
{
    public const string SectionName = "DisciplineOutbox";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 64;

    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(2);

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Background worker that drains <c>discipline.outbox_messages</c> and produces each
/// row to Kafka. Concurrency-safe across replicas: every batch is locked with
/// <c>SELECT ... FOR UPDATE SKIP LOCKED</c> inside a Postgres transaction, so two
/// relay instances pull disjoint slices instead of fighting for the same rows.
/// </summary>
public sealed class DisciplineOutboxRelay(
    IServiceScopeFactory scopeFactory,
    IKafkaPublisher publisher,
    IOptions<DisciplineOutboxRelayOptions> options,
    ILogger<DisciplineOutboxRelay> logger,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await DrainOnceAsync(opts, stoppingToken).ConfigureAwait(false);
                if (processed == 0)
                {
                    await Task.Delay(opts.PollInterval, clock, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is DbUpdateException or ProduceException<string, string>
                or KafkaException or InvalidOperationException)
            {
                logger.LogError(ex, "[DisciplineOutboxRelay] cycle failed, will retry after poll interval");
                await Task.Delay(opts.PollInterval, clock, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<int> DrainOnceAsync(DisciplineOutboxRelayOptions opts, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DisciplineDbContext>();

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var now = clock.GetUtcNow();
        // SKIP LOCKED keeps the worker horizontally scalable: replicas grab disjoint
        // batches without coordinating. Filter on next_attempt_at_utc honours the
        // exponential backoff baked into RecordFailure.
        var batch = await dbContext.OutboxMessages
            .FromSqlInterpolated($@"
                SELECT * FROM ""disciplines"".""outbox_messages""
                WHERE published_at_utc IS NULL
                  AND (next_attempt_at_utc IS NULL OR next_attempt_at_utc <= {now})
                ORDER BY occurred_at_utc
                LIMIT {opts.BatchSize}
                FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (batch.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        foreach (var message in batch)
        {
            try
            {
                await publisher
                    .PublishSerializedAsync(message.Topic, message.Key, message.Payload, cancellationToken)
                    .ConfigureAwait(false);
                message.MarkPublished(clock.GetUtcNow());
            }
            catch (Exception ex) when (ex is ProduceException<string, string> or KafkaException or TimeoutException)
            {
                var backoff = ComputeBackoff(opts, message.Attempts);
                message.RecordFailure(clock.GetUtcNow(), ex.Message, backoff);
                logger.LogWarning(
                    ex,
                    "[DisciplineOutboxRelay] Failed to publish {EventType} (attempt {Attempt}); retry in {Backoff}",
                    message.EventType,
                    message.Attempts,
                    backoff);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return batch.Count;
    }

    private static TimeSpan ComputeBackoff(DisciplineOutboxRelayOptions opts, int attempts)
    {
        // attempts is post-increment (already +1), so first failure -> base * 2^0
        var exponent = Math.Max(0, attempts - 1);
        var multiplier = Math.Pow(2, Math.Min(exponent, 10));
        var raw = opts.InitialBackoff * multiplier;
        return raw > opts.MaxBackoff ? opts.MaxBackoff : raw;
    }
}
