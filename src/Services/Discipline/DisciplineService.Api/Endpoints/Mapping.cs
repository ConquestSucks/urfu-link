using DisciplineService.Api.Application.Contracts.Responses;
using DisciplineService.Api.Application.Authorization;
using DisciplineService.Api.Domain.Aggregates;
using System.Security.Claims;

namespace DisciplineService.Api.Endpoints;

internal static class DisciplineMapping
{
    public static DisciplineResponse ToResponse(
        this Discipline discipline,
        ClaimsPrincipal principal,
        DisciplineAuthorizationService authorization)
    {
        ArgumentNullException.ThrowIfNull(discipline);

        var enrollments = discipline.Enrollments
            .OrderBy(e => e.Role)
            .ThenBy(e => e.EnrolledAtUtc)
            .Select(e => new EnrollmentResponse(e.UserId, e.Role, e.SubgroupId, e.EnrolledAtUtc, e.EnrolledBy))
            .ToList();
        var subgroups = discipline.Subgroups
            .OrderBy(s => s.ArchivedAtUtc.HasValue)
            .ThenBy(s => s.Name)
            .Select(s => new DisciplineSubgroupResponse(
                s.Id,
                s.Name,
                s.CreatedAtUtc,
                s.UpdatedAtUtc,
                s.ArchivedAtUtc))
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
            subgroups,
            PermissionsFor(discipline, principal, authorization),
            enrollments);
    }

    public static DisciplineListItem ToListItem(
        this Discipline discipline,
        ClaimsPrincipal principal,
        DisciplineAuthorizationService authorization)
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
            discipline.ArchivedAtUtc,
            PermissionsFor(discipline, principal, authorization));
    }

    public static DisciplineSubgroupResponse ToResponse(this DisciplineSubgroup subgroup)
        => new(
            subgroup.Id,
            subgroup.Name,
            subgroup.CreatedAtUtc,
            subgroup.UpdatedAtUtc,
            subgroup.ArchivedAtUtc);

    private static DisciplinePermissionsResponse PermissionsFor(
        Discipline discipline,
        ClaimsPrincipal principal,
        DisciplineAuthorizationService authorization)
    {
        return new DisciplinePermissionsResponse(
            CanUpdate: false,
            CanArchive: false,
            CanManageEnrollments: false,
            CanManageSubgroups: false);
    }
}
