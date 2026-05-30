using System.Net;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Domain.Exceptions;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class UnenrollUserRequest
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }
}

public sealed class UnenrollUserEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<UnenrollUserRequest>
{
    public override void Configure()
    {
        Delete("{id:guid}/enrollments/{userId:guid}");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Remove a user from a discipline (admin or owner teacher).");
    }

    public override async Task HandleAsync(UnenrollUserRequest req, CancellationToken ct)
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
            discipline.Unenroll(req.UserId);
        }
        catch (EnrollmentNotFoundException ex)
        {
            AddError("UserId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.NotFound, cancellation: ct).ConfigureAwait(false);
            return;
        }
        catch (OwnerEnrollmentRemovalException ex)
        {
            AddError("UserId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }
        catch (LastTeacherRemovalException ex)
        {
            AddError("UserId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }

        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
