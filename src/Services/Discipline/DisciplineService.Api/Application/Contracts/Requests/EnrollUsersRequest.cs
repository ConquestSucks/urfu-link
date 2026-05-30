using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.Api.Application.Contracts.Requests;

public sealed record EnrollmentInput(Guid UserId, DisciplineRole Role, Guid? SubgroupId = null);
