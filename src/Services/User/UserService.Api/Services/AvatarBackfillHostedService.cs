using Microsoft.Extensions.Options;

namespace UserService.Api.Services;

public sealed class AvatarBackfillHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<AvatarBackfillOptions> options,
    ILogger<AvatarBackfillHostedService> logger) : BackgroundService
{
    private readonly AvatarBackfillOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogDebug("Avatar backfill hosted service is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<AvatarBackfillService>();
                await service.RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // The background loop must retry after transient storage or database failures.
            catch (Exception ex)
            {
                logger.LogError(ex, "Avatar backfill run failed.");
            }
#pragma warning restore CA1031

            var interval = TimeSpan.FromMinutes(Math.Clamp(_options.IntervalMinutes, 5, 24 * 60));
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
