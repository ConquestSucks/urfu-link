using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class DeleteDisciplineRequest
{
    public Guid Id { get; init; }
}

public sealed class DeleteDisciplineEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<DeleteDisciplineRequest>
{
    public override void Configure()
    {
        Delete("{id:guid}");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Archive (soft delete) a discipline (admin only).");
    }

    public override async Task HandleAsync(DeleteDisciplineRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (!authorization.CanDelete(User))
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        var discipline = await repository.GetByIdAsync(req.Id, ct).ConfigureAwait(false);
        if (discipline is null)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        discipline.Archive();
        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
