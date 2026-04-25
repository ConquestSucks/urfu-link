using FastEndpoints;

namespace Urfu.Link.Services.Presence.Endpoints;

public sealed class PresenceGroup : Group
{
    public PresenceGroup()
    {
        Configure("presence", ep => ep.Description(b => b.RequireAuthorization()));
    }
}
