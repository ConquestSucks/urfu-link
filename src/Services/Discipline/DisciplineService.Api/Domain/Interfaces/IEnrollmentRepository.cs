using DisciplineService.Api.Domain.Aggregates;

namespace DisciplineService.Api.Domain.Interfaces;

public sealed record DisciplineFilter(
    string? Semester,
    Guid? UserId);

public sealed record DisciplineMembership(
    Guid DisciplineId,
    string Code,
    string Title,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId,
    Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineRole Role);

public interface IEnrollmentRepository
{
    Task<IReadOnlyList<Discipline>> ListDisciplinesAsync(
        DisciplineFilter filter,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DisciplineMembership>> ListMembershipsAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid disciplineId, Guid userId, CancellationToken cancellationToken);
}
