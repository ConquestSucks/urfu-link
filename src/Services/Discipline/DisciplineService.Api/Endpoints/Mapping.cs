using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Domain.Aggregates;

namespace DisciplineService.Api.Endpoints;

internal static class DisciplineMapping
{
    public static DisciplineResponse ToResponse(this Discipline discipline)
    {
        ArgumentNullException.ThrowIfNull(discipline);

        var enrollments = discipline.Enrollments
            .OrderBy(e => e.Role)
            .ThenBy(e => e.EnrolledAtUtc)
            .Select(e => new EnrollmentResponse(e.UserId, e.Role, e.EnrolledAtUtc, e.EnrolledBy))
            .ToList();

        return new DisciplineResponse(
            discipline.Id,
            discipline.Code,
            discipline.Title,
            discipline.Description,
            discipline.Semester,
            discipline.OwnerTeacherId,
            discipline.CoverAssetId,
            discipline.CreatedAtUtc,
            discipline.UpdatedAtUtc,
            discipline.ArchivedAtUtc,
            enrollments);
    }

    public static DisciplineListItem ToListItem(this Discipline discipline)
    {
        ArgumentNullException.ThrowIfNull(discipline);
        return new DisciplineListItem(
            discipline.Id,
            discipline.Code,
            discipline.Title,
            discipline.Semester,
            discipline.OwnerTeacherId,
            discipline.CoverAssetId,
            discipline.CreatedAtUtc,
            discipline.ArchivedAtUtc);
    }
}
