using DisciplineService.Api.Endpoints;
using FastEndpoints;
using FluentValidation;

namespace DisciplineService.Api.Application.Validators;

public sealed class ChangeRoleValidator : Validator<ChangeEnrollmentRoleRouteRequest>
{
    public ChangeRoleValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
