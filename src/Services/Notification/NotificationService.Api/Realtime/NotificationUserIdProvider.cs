using Microsoft.AspNetCore.SignalR;

namespace Urfu.Link.Services.Notification.Realtime;

public sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.User.FindFirst("sub")?.Value;
    }
}
