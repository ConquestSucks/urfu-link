using MediaService.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MediaService.Api.Workers;

/// <summary>
/// Periodically reaps expired upload sessions and the orphan MinIO objects the
/// client never finished uploading. Runs every 10 minutes by default.
/// </summary>
public sealed class UploadSessionCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<UploadSessionCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private const int BatchLimit = 100;

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
                logger.LogError(ex, "UploadSessionCleanupWorker iteration failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
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
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IUploadSessionRepository>();
        var assetRepo = scope.ServiceProvider.GetRequiredService<IMediaAssetRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IMediaObjectStorage>();

        var expired = await sessionRepo.GetExpiredAsync(DateTimeOffset.UtcNow, BatchLimit, cancellationToken)
            .ConfigureAwait(false);
        if (expired.Count == 0) return;

        logger.LogInformation("Cleaning up expired upload sessions");

        foreach (var session in expired)
        {
            var asset = await assetRepo.GetByIdAsync(session.AssetId, cancellationToken).ConfigureAwait(false);
            if (asset is not null && asset.State == Domain.Enums.AssetState.Initiated)
            {
                // Best-effort delete: object may not have been uploaded at all.
                try
                {
                    await storage.DeleteAsync(asset.Bucket, asset.ObjectKey, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex,
                        "Failed to delete orphan object {Bucket}/{Key} for asset {AssetId}",
                        asset.Bucket, asset.ObjectKey, asset.Id);
                }
                asset.MarkFailed();
            }
            sessionRepo.Remove(session);
        }

        await sessionRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await assetRepo.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
