using System.Net;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class UpdateDisciplineRouteRequest
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Semester { get; set; } = string.Empty;

    public Guid? CoverAssetId { get; set; }
}

public sealed class UpdateDisciplineEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<UpdateDisciplineRouteRequest>
{
    public override void Configure()
    {
        Put("{id:guid}");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Update discipline metadata (admin or owner teacher).");
    }

    public override async Task HandleAsync(UpdateDisciplineRouteRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var discipline = await repository.GetByIdAsync(req.Id, ct).ConfigureAwait(false);
        if (discipline is null)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        if (!authorization.CanModify(User, discipline))
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(discipline.Code, req.Code.Trim(), StringComparison.Ordinal)
            && await repository.CodeExistsAsync(req.Code, excludeDisciplineId: discipline.Id, ct).ConfigureAwait(false))
        {
            AddError(r => r.Code, "A discipline with this code already exists.");
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }

        discipline.Update(req.Code, req.Title, req.Description, req.Semester, req.CoverAssetId);

        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
