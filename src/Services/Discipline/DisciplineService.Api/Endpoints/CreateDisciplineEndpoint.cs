using System.Net;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Application.Contracts.Requests;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Auth;
using FastEndpoints;
using Urfu.Link.BuildingBlocks.Idempotency;

namespace DisciplineService.Api.Endpoints;

public sealed class CreateDisciplineEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<CreateDisciplineRequest, DisciplineResponse>
{
    public override void Configure()
    {
        Post(string.Empty);
        Group<DisciplinesGroup>();
        // Idempotency-Key is mandatory for create: a network blip on the client must
        // not produce two disciplines with otherwise-different generated codes.
        Options(x => x.AddEndpointFilter<IdempotencyEndpointFilter>());
        Summary(s => s.Summary = "Create a new discipline (admin only). Requires Idempotency-Key header.");
    }

    public override async Task HandleAsync(CreateDisciplineRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        if (!authorization.CanCreate(User))
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        if (await repository.CodeExistsAsync(req.Code, excludeDisciplineId: null, ct).ConfigureAwait(false))
        {
            AddError(r => r.Code, "A discipline with this code already exists.");
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }

        User.TryGetUserId(out var initiatedBy);
        var discipline = Discipline.CreateNew(
            req.Code,
            req.Title,
            req.Description,
            req.Semester,
            req.OwnerTeacherId,
            req.CoverAssetId,
            initiatedBy);

        repository.Add(discipline);
        await repository.SaveChangesAsync(ct).ConfigureAwait(false);

        var response = discipline.ToResponse();
        await Send.CreatedAtAsync<GetDisciplineEndpoint>(
            new { id = discipline.Id },
            response,
            cancellation: ct).ConfigureAwait(false);
    }
}
