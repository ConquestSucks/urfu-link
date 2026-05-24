using FastEndpoints;

namespace MediaService.Api.Endpoints;

/// <summary>
/// Single source of truth for the public REST surface under /media:
/// route prefix and the global authorization requirement live here so
/// individual endpoints cannot accidentally drop authentication when
/// edited.
/// </summary>
public sealed class MediaGroup : Group
{
    public MediaGroup()
    {
        Configure("media", ep =>
        {
            ep.Description(b => b.RequireAuthorization());
        });
    }
}
