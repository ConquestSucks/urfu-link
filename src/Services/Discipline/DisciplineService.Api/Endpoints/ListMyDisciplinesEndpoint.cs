using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Auth;
using FastEndpoints;

namespace DisciplineService.Api.Endpoints;

public sealed class ListMyDisciplinesResponse
{
    public IReadOnlyList<MyDisciplineResponse> Items { get; init; } = [];
}

public sealed class ListMyDisciplinesEndpoint(IEnrollmentRepository enrollments)
    : EndpointWithoutRequest<ListMyDisciplinesResponse>
{
    public override void Configure()
    {
        Get("me");
        Group<DisciplinesGroup>();
        Summary(s => s.Summary = "List disciplines the caller is enrolled in.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!User.TryGetUserId(out var userId))
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        var memberships = await enrollments.ListMembershipsAsync(userId, ct).ConfigureAwait(false);
        var items = memberships
            .Select(m => new MyDisciplineResponse(
                m.DisciplineId,
                m.Code,
                m.Title,
                m.Semester,
                m.OwnerTeacherId,
                m.CoverAssetId,
                m.Role,
                m.SubgroupId))
            .ToList();

        await Send.OkAsync(new ListMyDisciplinesResponse { Items = items }, cancellation: ct).ConfigureAwait(false);
    }
}
