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
    IReadOnlyList<EnrollmentResponse> Enrollments);

public sealed record EnrollmentResponse(
    Guid UserId,
    DisciplineRole Role,
    DateTimeOffset EnrolledAtUtc,
    Guid EnrolledBy);

public sealed record DisciplineListItem(
    Guid Id,
    string Code,
    string Title,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ArchivedAtUtc);

public sealed record MyDisciplineResponse(
    Guid Id,
    string Code,
    string Title,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId,
    DisciplineRole Role);
