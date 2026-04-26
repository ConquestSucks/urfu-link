using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Auth;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class ListDisciplinesQueryParams
{
    public string? Semester { get; init; }

    public bool IncludeArchived { get; init; }
}

public sealed class ListDisciplinesResponse
{
    public IReadOnlyList<DisciplineListItem> Items { get; init; } = [];
}

public sealed class ListDisciplinesEndpoint(IEnrollmentRepository enrollments)
    : Endpoint<ListDisciplinesQueryParams, ListDisciplinesResponse>
{
    public override void Configure()
    {
        Get(string.Empty);
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "List disciplines visible to the caller. Admin sees all; teachers/students see those they are enrolled in.");
    }

    public override async Task HandleAsync(ListDisciplinesQueryParams req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var includeArchived = req.IncludeArchived && User.IsAdmin();
        Guid? userScope = null;
        if (!User.IsAdmin())
        {
            if (!User.TryGetUserId(out var userId))
            {
                await Send.ForbiddenAsync(ct).ConfigureAwait(false);
                return;
            }

            userScope = userId;
        }

        var filter = new DisciplineFilter(req.Semester, userScope);
        var items = await enrollments.ListDisciplinesAsync(filter, includeArchived, ct).ConfigureAwait(false);

        await Send.OkAsync(
            new ListDisciplinesResponse
            {
                Items = items.Select(d => d.ToListItem()).ToList(),
            },
            cancellation: ct).ConfigureAwait(false);
    }
}
