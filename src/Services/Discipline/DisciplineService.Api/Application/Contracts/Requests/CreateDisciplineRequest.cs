namespace DisciplineService.Api.Application.Contracts.Requests;

public sealed record CreateDisciplineRequest(
    string Code,
    string Title,
    string? Description,
    string Semester,
    Guid OwnerTeacherId,
    Guid? CoverAssetId);
