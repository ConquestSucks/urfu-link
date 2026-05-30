using DisciplineService.Api.Endpoints;
using FastEndpoints;
using FluentValidation;

namespace DisciplineService.Api.Application.Validators;

public sealed class CreateSubgroupValidator : Validator<CreateSubgroupRequest>
{
    public CreateSubgroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

public sealed class UpdateSubgroupValidator : Validator<UpdateSubgroupRequest>
{
    public UpdateSubgroupValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
    }
}

public sealed class AssignEnrollmentSubgroupValidator : Validator<AssignEnrollmentSubgroupRequest>
{
    public AssignEnrollmentSubgroupValidator()
    {
        RuleFor(x => x.SubgroupId).NotEqual(Guid.Empty);
    }
}
