using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Exceptions;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class UpdateSubgroupRequest
{
    public Guid Id { get; set; }

    public Guid SubgroupId { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class UpdateSubgroupEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<UpdateSubgroupRequest, DisciplineSubgroupResponse>
{
    public override void Configure()
    {
        Patch("{id:guid}/subgroups/{subgroupId:guid}");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Rename a discipline subgroup.");
    }

    public override async Task HandleAsync(UpdateSubgroupRequest req, CancellationToken ct)
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
            discipline.RenameSubgroup(req.SubgroupId, req.Name);
        }
        catch (DisciplineSubgroupNotFoundException)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        var subgroup = discipline.Subgroups.Single(s => s.Id == req.SubgroupId);
        await Send.OkAsync(subgroup.ToResponse(), ct).ConfigureAwait(false);
    }
}
