using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class GetDisciplineRequest
{
    public Guid Id { get; init; }
}

public sealed class GetDisciplineEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<GetDisciplineRequest, DisciplineResponse>
{
    public override void Configure()
    {
        Get("{id:guid}");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Retrieve a discipline with its enrollments.");
    }

    public override async Task HandleAsync(GetDisciplineRequest req, CancellationToken ct)
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

        await Send.OkAsync(discipline.ToResponse(User, authorization), cancellation: ct).ConfigureAwait(false);
    }
}
