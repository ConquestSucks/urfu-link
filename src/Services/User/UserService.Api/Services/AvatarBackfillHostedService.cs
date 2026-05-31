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

        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AvatarBackfillService>();
        await service.RunOnceAsync(stoppingToken).ConfigureAwait(false);
    }
}
