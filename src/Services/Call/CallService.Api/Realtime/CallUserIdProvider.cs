using Microsoft.AspNetCore.SignalR;

namespace Urfu.Link.Services.Call.Realtime;

public sealed class CallUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.User?.FindFirst("sub")?.Value;
    }
}
