using MediaService.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaService.Api.Workers;

public sealed class RetentionWorkerOptions
{
    public const string SectionName = "Retention";
    public TimeSpan SoftDeleteTtl { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(24);
    public int BatchLimit { get; set; } = 500;
}

/// <summary>
/// Hard-deletes assets that have been soft-deleted longer than the retention TTL
/// (default 30 days). Removes the MinIO object first, then transitions the asset
/// to HardDeleted, which raises <see cref="Domain.Events.MediaAssetHardDeletedEvent"/>.
/// </summary>
public sealed class RetentionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RetentionWorkerOptions> options,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException)
            {
                logger.LogError(ex, "RetentionWorker iteration failed");
            }

            try
            {
                await Task.Delay(options.Value.SweepInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var assetRepo = scope.ServiceProvider.GetRequiredService<IMediaAssetRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IMediaObjectStorage>();

        var cutoff = DateTimeOffset.UtcNow - options.Value.SoftDeleteTtl;
        var due = await assetRepo.GetForRetentionAsync(cutoff, options.Value.BatchLimit, cancellationToken)
            .ConfigureAwait(false);
        if (due.Count == 0) return;

        logger.LogInformation("Retention sweep: hard-deleting due assets older than {Cutoff}", cutoff);

        foreach (var asset in due)
        {
            try
            {
                await storage.DeleteAsync(asset.Bucket, asset.ObjectKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Failed to hard-delete object {Bucket}/{Key} for asset {AssetId}; skipping for this sweep",
                    asset.Bucket, asset.ObjectKey, asset.Id);
                continue;
            }
            asset.HardDelete();
        }

        await assetRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
