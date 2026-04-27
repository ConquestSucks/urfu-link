using Microsoft.Extensions.Diagnostics.HealthChecks;
using Yarp.ReverseProxy.Configuration;

namespace Urfu.Link.Gateway.ApiGateway.HealthChecks;

/// <summary>
/// Aggregates downstream cluster availability into a single readiness signal.
/// For each cluster, probes <c>/health/ready</c> on every configured destination; cluster is considered usable if at
/// least one destination responded within the timeout. Result is cached to amortise overhead across kubelet probes.
/// </summary>
public sealed class YarpDestinationsHealthCheck : IHealthCheck, IDisposable
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    private readonly IProxyConfigProvider _configProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private DateTimeOffset _cacheExpiresAt = DateTimeOffset.MinValue;
    private HealthCheckResult _cachedResult = HealthCheckResult.Healthy("not yet probed");

    public YarpDestinationsHealthCheck(
        IProxyConfigProvider configProvider,
        IHttpClientFactory httpClientFactory)
    {
        _configProvider = configProvider;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (DateTimeOffset.UtcNow < _cacheExpiresAt)
        {
            return _cachedResult;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DateTimeOffset.UtcNow < _cacheExpiresAt)
            {
                return _cachedResult;
            }

            _cachedResult = await ProbeAsync(cancellationToken).ConfigureAwait(false);
            _cacheExpiresAt = DateTimeOffset.UtcNow.Add(CacheDuration);
            return _cachedResult;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<HealthCheckResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var config = _configProvider.GetConfig();
        var clusters = config.Clusters;
        if (clusters is null || clusters.Count == 0)
        {
            return HealthCheckResult.Healthy("No clusters configured.");
        }

        var unhealthy = new List<string>();
        var probes = clusters
            .Select(cluster => ProbeClusterAsync(cluster, cancellationToken))
            .ToArray();

        var results = await Task.WhenAll(probes).ConfigureAwait(false);
        for (var i = 0; i < clusters.Count; i++)
        {
            if (!results[i])
            {
                unhealthy.Add(clusters[i].ClusterId);
            }
        }

        if (unhealthy.Count == 0)
        {
            return HealthCheckResult.Healthy(
                $"All {clusters.Count} downstream clusters have at least one reachable destination.");
        }

        return HealthCheckResult.Degraded(
            $"Clusters with no reachable destinations: {string.Join(", ", unhealthy)}.",
            data: new Dictionary<string, object> { ["unhealthyClusters"] = unhealthy });
    }

    private async Task<bool> ProbeClusterAsync(ClusterConfig cluster, CancellationToken cancellationToken)
    {
        var destinations = cluster.Destinations;
        if (destinations is null || destinations.Count == 0)
        {
            return false;
        }

        foreach (var (_, destination) in destinations)
        {
            if (string.IsNullOrWhiteSpace(destination.Address))
            {
                continue;
            }

            if (await IsReachableAsync(destination.Address, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> IsReachableAsync(string address, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var baseUri))
        {
            return false;
        }

        var probeUri = new Uri(baseUri, "/health/ready");
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = ProbeTimeout;

        try
        {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probeCts.CancelAfter(ProbeTimeout);
            using var response = await client
                .GetAsync(probeUri, HttpCompletionOption.ResponseHeadersRead, probeCts.Token)
                .ConfigureAwait(false);
            return (int)response.StatusCode < 500;
        }
#pragma warning disable CA1031 // Health probes intentionally swallow exceptions to mark destination unhealthy.
        catch
#pragma warning restore CA1031
        {
            return false;
        }
    }

    public void Dispose() => _refreshLock.Dispose();
}
