using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Urfu.Link.BuildingBlocks.ServiceDefaults;

/// <summary>
/// Reports whether the Redis connection underlying the SignalR backplane is established.
/// SignalR clients can connect to this replica regardless of backplane state, but broadcasts
/// will not propagate cross-replica until the connection is up — so the service must not be
/// marked Ready until the backplane is connected.
/// </summary>
public sealed class SignalRBackplaneHealthCheck(IConnectionMultiplexer multiplexer) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (multiplexer.IsConnected)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                $"Redis backplane connected ({multiplexer.GetEndPoints().Length} endpoint(s))."));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy(
            "Redis backplane disconnected — SignalR broadcasts will not propagate cross-replica."));
    }
}

public static class SignalRBackplaneHealthCheckExtensions
{
    /// <summary>
    /// Registers a readiness check that reports Unhealthy while the SignalR Redis backplane
    /// connection is not established. Reuses the singleton <see cref="IConnectionMultiplexer"/>
    /// already wired by <see cref="ServiceDefaultsExtensions.AddServiceDefaults"/> via
    /// <c>AddIdempotency</c>.
    /// </summary>
    public static IHealthChecksBuilder AddSignalRBackplaneHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "signalr-backplane")
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => new SignalRBackplaneHealthCheck(sp.GetRequiredService<IConnectionMultiplexer>()),
            HealthStatus.Unhealthy,
            ["ready"]));
    }
}
