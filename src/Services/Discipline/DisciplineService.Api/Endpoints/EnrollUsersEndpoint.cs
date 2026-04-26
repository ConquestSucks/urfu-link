using System.Net;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Application.Contracts.Requests;
using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Exceptions;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Auth;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class EnrollUsersRouteRequest
{
    public Guid Id { get; set; }

    public IReadOnlyList<EnrollmentInput> Enrollments { get; set; } = [];
}

public sealed class EnrollUsersResponse
{
    public IReadOnlyList<EnrollmentResponse> Enrollments { get; init; } = [];
}

public sealed class EnrollUsersEndpoint(
    IDisciplineRepository repository,
    DisciplineAuthorizationService authorization)
    : Endpoint<EnrollUsersRouteRequest, EnrollUsersResponse>
{
    public override void Configure()
    {
        Post("{id:guid}/enrollments");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "Batch enroll users into a discipline (admin or owner teacher).");
    }

    public override async Task HandleAsync(EnrollUsersRouteRequest req, CancellationToken ct)
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

        User.TryGetUserId(out var actorId);
        var added = new List<Domain.Aggregates.Enrollment>(req.Enrollments.Count);
        for (var i = 0; i < req.Enrollments.Count; i++)
        {
            var input = req.Enrollments[i];
            try
            {
                var enrollment = discipline.Enroll(input.UserId, input.Role, actorId);
                added.Add(enrollment);
            }
            catch (EnrollmentExistsException ex)
            {
                AddError($"Enrollments[{i}].UserId", ex.Message);
            }
        }

        if (ValidationFailures.Count > 0)
        {
            await Send.ErrorsAsync(statusCode: (int)HttpStatusCode.Conflict, cancellation: ct).ConfigureAwait(false);
            return;
        }

        await repository.SaveChangesAsync(ct).ConfigureAwait(false);

        await Send.OkAsync(
            new EnrollUsersResponse
            {
                Enrollments = added
                    .Select(e => new EnrollmentResponse(e.UserId, e.Role, e.EnrolledAtUtc, e.EnrolledBy))
                    .ToList(),
            },
            cancellation: ct).ConfigureAwait(false);
    }
}
