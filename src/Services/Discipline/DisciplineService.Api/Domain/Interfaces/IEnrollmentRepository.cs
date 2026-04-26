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

/// <summary>
/// Cursor for keyset-paginated enrollment listings: a tuple of the last seen
/// EnrolledAtUtc + EnrollmentId. Stable under inserts because the page advances
/// on the unique <see cref="Enrollment.Id"/>.
/// </summary>
public sealed record EnrollmentCursor(DateTimeOffset EnrolledAtUtc, Guid EnrollmentId);

public sealed record EnrollmentPage(
    IReadOnlyList<Enrollment> Items,
    EnrollmentCursor? NextCursor);

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

    /// <summary>
    /// Returns up to <paramref name="limit"/> enrollments for <paramref name="disciplineId"/>
    /// ordered by <c>(EnrolledAtUtc asc, Id asc)</c>. <paramref name="cursor"/> resumes the
    /// previous page; <c>null</c> starts from the beginning. The returned <see cref="EnrollmentPage.NextCursor"/>
    /// is non-null when more rows remain.
    /// </summary>
    Task<EnrollmentPage> ListEnrollmentsAsync(
        Guid disciplineId,
        EnrollmentCursor? cursor,
        int limit,
        CancellationToken cancellationToken);
}
