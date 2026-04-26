using DisciplineService.Api.Application.Contracts.Requests;
using DisciplineService.Api.Endpoints;
using FastEndpoints;
using FluentValidation;

namespace DisciplineService.Api.Application.Validators;

public sealed class EnrollUsersValidator : Validator<EnrollUsersRouteRequest>
{
    public EnrollUsersValidator()
    {
        RuleFor(x => x.Enrollments)
            .NotNull()
            .NotEmpty()
            .Must(items => items.Count <= 500)
            .WithMessage("At most 500 enrollments per request.")
            .Must(HaveUniqueUserIds)
            .WithMessage("Duplicate user ids in enrollment batch.");

        RuleForEach(x => x.Enrollments).ChildRules(child =>
        {
            child.RuleFor(e => e.UserId).NotEqual(Guid.Empty);
            child.RuleFor(e => e.Role).IsInEnum();
        });
    }

    private static bool HaveUniqueUserIds(IReadOnlyList<EnrollmentInput> items)
        => items is not null && items.Select(i => i.UserId).Distinct().Count() == items.Count;
}
