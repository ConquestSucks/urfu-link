using DisciplineService.Api.Application.Contracts.Requests;
using DisciplineService.Api.Application.Validators;
using FluentValidation.TestHelper;

namespace DisciplineService.UnitTests.Validators;

public sealed class CreateDisciplineValidatorTests
{
    private readonly CreateDisciplineValidator _validator = new();

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        var req = new CreateDisciplineRequest("CS101", "Intro", "Desc", "2026-spring", Guid.NewGuid(), null);
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "Code")]
    [InlineData("   ", "Code")]
    public void BlankCode_Fails(string code, string field)
    {
        var req = new CreateDisciplineRequest(code, "Intro", null, "2026-spring", Guid.NewGuid(), null);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(field);
    }

    [Fact]
    public void CodeWithSpaces_Fails()
    {
        var req = new CreateDisciplineRequest("CS 101", "Intro", null, "2026-spring", Guid.NewGuid(), null);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.Code);
    }

    [Theory]
    [InlineData("CS-101")]
    [InlineData("CS_101")]
    [InlineData("CS.101")]
    [InlineData("intro2025")]
    public void ValidCodeFormats_Pass(string code)
    {
        var req = new CreateDisciplineRequest(code, "Intro", null, "2026-spring", Guid.NewGuid(), null);
        var result = _validator.TestValidate(req);
        result.ShouldNotHaveValidationErrorFor(r => r.Code);
    }

    [Fact]
    public void EmptyOwner_Fails()
    {
        var req = new CreateDisciplineRequest("CS101", "Intro", null, "2026-spring", Guid.Empty, null);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.OwnerTeacherId);
    }

    [Fact]
    public void TitleTooLong_Fails()
    {
        var req = new CreateDisciplineRequest("CS101", new string('x', 257), null, "2026-spring", Guid.NewGuid(), null);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(r => r.Title);
    }
}
