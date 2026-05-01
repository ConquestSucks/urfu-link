using System.Collections.Concurrent;
using Yarp.ReverseProxy.Model;

namespace Urfu.Link.Gateway.ApiGateway.HealthChecks;

/// <summary>
/// Singleton sink for YARP cluster state notifications. Captures clusters from
/// <see cref="IClusterChangeListener"/> events so readiness checks can read live destination
/// health (driven by YARP active probes) without issuing their own HTTP traffic.
/// </summary>
public sealed class YarpClusterStateRegistry : IClusterChangeListener
{
    private readonly ConcurrentDictionary<string, ClusterState> _clusters = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ClusterState> Clusters => (IReadOnlyCollection<ClusterState>)_clusters.Values;

    public void OnClusterAdded(ClusterState cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        _clusters[cluster.ClusterId] = cluster;
    }

    public void OnClusterChanged(ClusterState cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        _clusters[cluster.ClusterId] = cluster;
    }

    public void OnClusterRemoved(ClusterState cluster)
    {
        ArgumentNullException.ThrowIfNull(cluster);
        _clusters.TryRemove(cluster.ClusterId, out _);
    }
}
