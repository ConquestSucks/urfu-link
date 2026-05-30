using DisciplineService.Api.Endpoints;
using FastEndpoints;
using FluentValidation;

namespace DisciplineService.Api.Application.Validators;

public sealed class ChangeRoleValidator : Validator<ChangeEnrollmentRoleRouteRequest>
{
    public ChangeRoleValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.SubgroupId)
            .NotNull()
            .When(x => x.Role == Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineRole.Student)
            .WithMessage("Student role requires subgroupId.");
        RuleFor(x => x.SubgroupId)
            .Null()
            .When(x => x.Role == Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines.DisciplineRole.Teacher)
            .WithMessage("Teacher role must not include subgroupId.");
    }
}
