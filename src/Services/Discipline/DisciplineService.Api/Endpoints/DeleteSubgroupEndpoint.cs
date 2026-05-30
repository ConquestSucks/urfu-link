using System.Net;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Domain.Exceptions;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class DeleteSubgroupRequest
{
    public Guid Id { get; set; }

    public Guid SubgroupId { get; set; }
}

public sealed class DeleteSubgroupEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<DeleteSubgroupRequest>
{
    public override void Configure()
    {
        Delete("{id:guid}/subgroups/{subgroupId:guid}");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Archive an empty discipline subgroup.");
    }

    public override async Task HandleAsync(DeleteSubgroupRequest req, CancellationToken ct)
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

        try
        {
            discipline.ArchiveSubgroup(req.SubgroupId);
        }
        catch (DisciplineSubgroupNotFoundException)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }
        catch (DisciplineSubgroupNotEmptyException ex)
        {
            AddError("SubgroupId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }

        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
