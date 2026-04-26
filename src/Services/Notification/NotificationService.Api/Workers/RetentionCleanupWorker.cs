using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace Urfu.Link.Services.Notification.Workers;

public sealed class RetentionOptions
{
    public const string SectionName = "Notification:Retention";

    public int RetentionDays { get; set; } = 90;

    public int FuturePartitionsToProvision { get; set; } = 2;

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromHours(6);
}

public sealed class RetentionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionOptions> options,
    ILogger<RetentionCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        // Run once at startup so a fresh deployment provisions the upcoming partitions.
        await TickAsync(settings, stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(settings.PollInterval);
        try
        {
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await TickAsync(settings, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TickAsync(RetentionOptions settings, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<PartitionManager>();
            var now = scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow();

            await EnsureUpcomingAsync(manager, now, settings.FuturePartitionsToProvision, cancellationToken).ConfigureAwait(false);
            await DropExpiredAsync(manager, now, settings.RetentionDays, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Retention cleanup tick failed");
        }
    }

    private static async Task EnsureUpcomingAsync(
        PartitionManager manager,
        DateTimeOffset now,
        int monthsAhead,
        CancellationToken cancellationToken)
    {
        var current = YearMonth.FromUtc(now);
        for (var offset = 0; offset <= monthsAhead; offset++)
        {
            await manager.EnsureAsync(current.AddMonths(offset), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task DropExpiredAsync(
        PartitionManager manager,
        DateTimeOffset now,
        int retentionDays,
        CancellationToken cancellationToken)
    {
        var cutoff = YearMonth.FromUtc(now.AddDays(-retentionDays));
        var existing = await manager.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var month in existing)
        {
            if (month < cutoff)
            {
                await manager.DropAsync(month, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
