namespace Urfu.Link.Services.Notification.Infrastructure.Grpc;

/// <summary>
/// Configuration for the gRPC client that fetches presence state from PresenceService.
/// Bound from configuration section <c>PresenceService</c> (env: <c>PresenceService__GrpcEndpoint</c>).
/// </summary>
public sealed class PresenceServiceClientOptions
{
    public const string SectionName = "PresenceService";

    /// <summary>
    /// gRPC endpoint of PresenceService. Empty string disables the gRPC client and falls
    /// back to <c>OfflinePresenceClient</c> — appropriate for tests and the on-prem profile
    /// where presence is intentionally not deployed.
    /// </summary>
    public string GrpcEndpoint { get; set; } = string.Empty;
}
