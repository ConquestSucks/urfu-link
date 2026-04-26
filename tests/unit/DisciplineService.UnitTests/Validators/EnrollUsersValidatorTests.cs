using DisciplineService.Api.Application.Contracts.Requests;
using DisciplineService.Api.Application.Validators;
using DisciplineService.Api.Endpoints;
using FluentValidation.TestHelper;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Disciplines;

namespace DisciplineService.UnitTests.Validators;

public sealed class EnrollUsersValidatorTests
{
    private readonly EnrollUsersValidator _validator = new();

    [Fact]
    public void NonEmptyUniqueBatch_Passes()
    {
        var req = new EnrollUsersRouteRequest
        {
            Id = Guid.NewGuid(),
            Enrollments =
            [
                new EnrollmentInput(Guid.NewGuid(), DisciplineRole.Student),
                new EnrollmentInput(Guid.NewGuid(), DisciplineRole.Teacher),
            ],
        };

        var result = _validator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyBatch_Fails()
    {
        var req = new EnrollUsersRouteRequest { Id = Guid.NewGuid(), Enrollments = [] };
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.Enrollments);
    }

    [Fact]
    public void DuplicateUserIds_Fails()
    {
        var dupId = Guid.NewGuid();
        var req = new EnrollUsersRouteRequest
        {
            Id = Guid.NewGuid(),
            Enrollments =
            [
                new EnrollmentInput(dupId, DisciplineRole.Student),
                new EnrollmentInput(dupId, DisciplineRole.Teacher),
            ],
        };

        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.Enrollments);
    }

    [Fact]
    public void EmptyUserId_Fails()
    {
        var req = new EnrollUsersRouteRequest
        {
            Id = Guid.NewGuid(),
            Enrollments = [new EnrollmentInput(Guid.Empty, DisciplineRole.Student)],
        };
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor("Enrollments[0].UserId");
    }

    [Fact]
    public void TooManyEnrollments_Fails()
    {
        var items = Enumerable.Range(0, 501)
            .Select(_ => new EnrollmentInput(Guid.NewGuid(), DisciplineRole.Student))
            .ToList();
        var req = new EnrollUsersRouteRequest { Id = Guid.NewGuid(), Enrollments = items };
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.Enrollments);
    }
}
