using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.Api.Application.Contracts.Responses;

public sealed record DisciplineResponse(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    IReadOnlyList<DisciplineSubgroupResponse> Subgroups,
    DisciplinePermissionsResponse Permissions,
    IReadOnlyList<EnrollmentResponse> Enrollments);

public sealed record EnrollmentResponse(
    Guid UserId,
    DisciplineRole Role,
    Guid? SubgroupId,
    DateTimeOffset EnrolledAtUtc,
    Guid EnrolledBy);

public sealed record DisciplineSubgroupResponse(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record DisciplinePermissionsResponse(
    bool CanUpdate,
    bool CanArchive,
    bool CanManageEnrollments,
    bool CanManageSubgroups);

public sealed record DisciplineListItem(
    Guid Id,
    string Code,
    string Title,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    DisciplinePermissionsResponse Permissions);

public sealed record MyDisciplineResponse(
    Guid Id,
    string Code,
    string Title,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId,
    DisciplineRole Role,
    Guid? SubgroupId);
