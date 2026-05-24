using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yarp.ReverseProxy.Model;

namespace Urfu.Link.Gateway.ApiGateway.HealthChecks;

/// <summary>
/// Reads destination health populated by YARP active probes (configured per-cluster in
/// <c>appsettings.json</c>) and aggregates it into a downstream dependency signal:
/// <list type="bullet">
///   <item><description><see cref="HealthStatus.Healthy"/> — every cluster has at least one destination whose Active or Passive health is not <see cref="DestinationHealth.Unhealthy"/>.</description></item>
///   <item><description><see cref="HealthStatus.Unhealthy"/> — at least one cluster has no usable destinations.</description></item>
/// </list>
/// Probes themselves are owned by YARP (<c>HealthCheck.Active</c> in cluster config); this check
/// does not issue HTTP traffic.
/// </summary>
public sealed class YarpDestinationsHealthCheck(YarpClusterStateRegistry registry) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var clusters = registry.Clusters;
        if (clusters.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("No clusters configured."));
        }

        var unhealthy = new List<string>();
        foreach (var cluster in clusters)
        {
            if (!HasUsableDestination(cluster))
            {
                unhealthy.Add(cluster.ClusterId);
            }
        }

        if (unhealthy.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                $"All {clusters.Count} downstream clusters have at least one usable destination."));
        }

        var data = new Dictionary<string, object>
        {
            ["unhealthyClusters"] = unhealthy,
        };
        return Task.FromResult(HealthCheckResult.Unhealthy(
            $"Clusters with no usable destinations: {string.Join(", ", unhealthy)}.",
            data: data));
    }

    private static bool HasUsableDestination(ClusterState cluster)
    {
        if (cluster.Destinations.IsEmpty)
        {
            return false;
        }

        foreach (var destination in cluster.Destinations.Values)
        {
            var health = destination.Health;
            if (health.Active != DestinationHealth.Unhealthy
                && health.Passive != DestinationHealth.Unhealthy)
            {
                return true;
            }
        }

        return false;
    }
}
