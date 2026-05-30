using System.Net;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Domain.Exceptions;
using DisciplineService.Api.Domain.Interfaces;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class AssignEnrollmentSubgroupRequest
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid SubgroupId { get; set; }
}

public sealed class AssignEnrollmentSubgroupEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<AssignEnrollmentSubgroupRequest>
{
    public override void Configure()
    {
        Patch("{id:guid}/enrollments/{userId:guid}/subgroup");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Move a student to another discipline subgroup.");
    }

    public override async Task HandleAsync(AssignEnrollmentSubgroupRequest req, CancellationToken ct)
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
            discipline.AssignStudentSubgroup(req.UserId, req.SubgroupId);
        }
        catch (EnrollmentNotFoundException)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }
        catch (DisciplineSubgroupNotFoundException ex)
        {
            AddError("SubgroupId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }
        catch (TeacherSubgroupNotAllowedException ex)
        {
            AddError("UserId", ex.Message);
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }

        await repository.SaveChangesAsync(ct).ConfigureAwait(false);
        await Send.NoContentAsync(ct).ConfigureAwait(false);
    }
}
