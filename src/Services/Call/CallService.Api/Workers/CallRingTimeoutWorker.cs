using Urfu.Link.Services.Call.Application.Calls;

namespace Urfu.Link.Services.Call.Workers;

public sealed class CallRingTimeoutWorker(
    ICallSessionStore sessions,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<CallRingTimeoutWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                var expired = await sessions
                    .ListExpiredRingingAsync(timeProvider.GetUtcNow(), limit: 50, stoppingToken)
                    .ConfigureAwait(false);
                foreach (var session in expired)
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<CallSessionService>();
                    await service.ProcessExpiredRingingAsync(session, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process expired ringing calls.");
            }
        }
    }
}
