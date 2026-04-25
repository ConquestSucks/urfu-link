using FastEndpoints;

namespace Urfu.Link.Services.Chat.Endpoints;

public sealed class ChatGroup : Group
{
    public ChatGroup()
    {
        Configure("chat", ep => ep.Description(b => b.RequireAuthorization()));
    }
}
