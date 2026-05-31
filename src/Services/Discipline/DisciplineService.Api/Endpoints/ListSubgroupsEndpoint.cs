using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class ListSubgroupsRequest
{
    public Guid Id { get; set; }

    public bool IncludeArchived { get; set; }
}

public sealed class ListSubgroupsResponse
{
    public IReadOnlyList<DisciplineSubgroupResponse> Items { get; init; } = [];
}

public sealed class ListSubgroupsEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<ListSubgroupsRequest, ListSubgroupsResponse>
{
    public override void Configure()
    {
        Get("{id:guid}/subgroups");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "List subgroups inside a discipline.");
    }

    public override async Task HandleAsync(ListSubgroupsRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var discipline = await repository.GetByIdAsync(req.Id, ct).ConfigureAwait(false);
        if (discipline is null)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        if (!authorization.CanRead(User, discipline))
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        var items = discipline.Subgroups
            .Where(s => req.IncludeArchived || !s.IsArchived)
            .OrderBy(s => s.ArchivedAtUtc.HasValue)
            .ThenBy(s => s.Name)
            .Select(s => s.ToResponse())
            .ToList();
        await Send.OkAsync(new ListSubgroupsResponse { Items = items }, ct).ConfigureAwait(false);
    }
}
