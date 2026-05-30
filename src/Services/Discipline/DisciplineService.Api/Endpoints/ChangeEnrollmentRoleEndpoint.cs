using System.Net;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Domain.Exceptions;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.Api.Endpoints;

public sealed class ChangeEnrollmentRoleRouteRequest
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DisciplineRole Role { get; set; }

    public Guid? SubgroupId { get; set; }
}

public sealed class ChangeEnrollmentRoleEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<ChangeEnrollmentRoleRouteRequest>
{
    public override void Configure()
    {
        Patch("{id:guid}/enrollments/{userId:guid}/role");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Change a user's role inside a discipline (admin or owner teacher).");
    }

    public override async Task HandleAsync(ChangeEnrollmentRoleRouteRequest req, CancellationToken ct)
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
            discipline.ChangeRole(req.UserId, req.Role, req.SubgroupId);
        }
        catch (EnrollmentNotFoundException ex)
        {
            AddError("UserId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.NotFound, cancellation: ct).ConfigureAwait(false);
            return;
        }
        catch (OwnerRoleChangeException ex)
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
        catch (StudentSubgroupRequiredException ex)
        {
            AddError("SubgroupId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }
        catch (TeacherSubgroupNotAllowedException ex)
        {
            AddError("SubgroupId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }
        catch (DisciplineSubgroupNotFoundException ex)
        {
            AddError("SubgroupId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }

        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
