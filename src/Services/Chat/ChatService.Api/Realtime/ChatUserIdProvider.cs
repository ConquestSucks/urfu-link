using Microsoft.AspNetCore.SignalR;

namespace Urfu.Link.Services.Chat.Realtime;

/// <summary>
/// Maps a SignalR connection to its user identifier by reading the JWT <c>sub</c> claim.
/// Required so <c>Clients.Users</c> can target a user across all of their devices.
/// </summary>
internal sealed class ChatUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.User?.FindFirst("sub")?.Value;
    }
}
