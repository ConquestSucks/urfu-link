using FastEndpoints;
using FluentValidation;
using UserService.Api.Application.Contracts.Requests;

namespace UserService.Api.Application.Validators;

public sealed class UpdateAccountValidator : Validator<UpdateAccountRequest>
{
    public UpdateAccountValidator()
    {
        RuleFor(x => x.AboutMe)
            .MaximumLength(500)
            .When(x => x.AboutMe is not null);
    }
}
