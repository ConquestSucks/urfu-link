using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class CreateSubgroupRequest
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

public sealed class CreateSubgroupEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<CreateSubgroupRequest, DisciplineSubgroupResponse>
{
    public override void Configure()
    {
        Post("{id:guid}/subgroups");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Create a subgroup inside a discipline.");
    }

    public override async Task HandleAsync(CreateSubgroupRequest req, CancellationToken ct)
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

        var subgroup = discipline.CreateSubgroup(req.Name);
        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.OkAsync(subgroup.ToResponse(), ct).ConfigureAwait(false);
    }
}
