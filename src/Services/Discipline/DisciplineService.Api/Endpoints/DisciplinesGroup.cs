using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class DisciplinesGroup : Group
{
    public DisciplinesGroup()
    {
        Configure("disciplines", ep =>
        {
            ep.Description(b => b.RequireAuthorization());
        });
    }
}
