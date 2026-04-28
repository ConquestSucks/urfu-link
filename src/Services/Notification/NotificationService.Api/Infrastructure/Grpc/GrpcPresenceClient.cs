using Grpc.Core;
using Microsoft.Extensions.Logging;
using Urfu.Link.Services.Notification.Application.Preferences;
using PresenceGrpc = Urfu.Link.Services.Presence.Grpc;

namespace Urfu.Link.Services.Notification.Infrastructure.Grpc;

/// <summary>
/// gRPC-backed implementation of <see cref="IPresenceClient"/>. Calls
/// <c>PresenceService.InternalApi.GetPresence</c> and reports the user as
/// "online on web" iff the aggregated status is <c>ONLINE</c> and the
/// <c>WEB</c> platform appears in the active session list.
/// </summary>
/// <remarks>
/// This is the contract that lets the notification router skip Push for chat
/// categories when the user is already receiving the in-app SignalR notification
/// on their web tab. RPC failures fail open (assume offline) so
/// presence outages never silently drop notifications — duplicate push for
/// online users is preferable to no push for offline users.
/// </remarks>
public sealed class GrpcPresenceClient(
    PresenceGrpc.InternalApi.InternalApiClient client,
    ILogger<GrpcPresenceClient> logger) : IPresenceClient
{
    public async Task<bool> IsOnlineOnWebAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var reply = await client.GetPresenceAsync(
                new PresenceGrpc.GetPresenceRequest { UserId = userId.ToString() },
                cancellationToken: cancellationToken);

            return IsOnlineOnWeb(reply);
        }
        catch (RpcException ex)
        {
            // Fail open: matches the behaviour of OfflinePresenceClient, so a presence
            // outage never silently drops a Push delivery.
            logger.LogWarning(ex, "Presence lookup failed for {UserId}; treating as offline.", userId);
            return false;
        }
    }

    /// <summary>
    /// Pure mapping from the protobuf reply to the boolean contract; exposed at internal scope
    /// so the unit tests can exercise every status × platform combination without booting a
    /// real gRPC server.
    /// </summary>
    internal static bool IsOnlineOnWeb(PresenceGrpc.PresenceInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return info.Status == PresenceGrpc.PresenceStatus.Online
            && info.Platforms.Contains(PresenceGrpc.Platform.Web);
    }
}
