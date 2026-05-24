using DisciplineService.Api.Endpoints;
using FastEndpoints;
using FluentValidation;

namespace DisciplineService.Api.Application.Validators;

public sealed class UpdateDisciplineValidator : Validator<UpdateDisciplineRouteRequest>
{
    public UpdateDisciplineValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(32)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("Code may contain letters, digits, '.', '_' or '-' only.");
        RuleFor(x => x.Title).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Semester).NotEmpty().MaximumLength(32);
    }
}
