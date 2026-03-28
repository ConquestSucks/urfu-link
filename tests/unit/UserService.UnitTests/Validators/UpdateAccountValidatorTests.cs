using FluentValidation.TestHelper;
using UserService.Api.Application.Contracts.Requests;
using UserService.Api.Application.Validators;

namespace UserService.UnitTests.Validators;

public sealed class UpdateAccountValidatorTests
{
    private readonly UpdateAccountValidator _validator = new();

    [Fact]
    public void ShouldPassWhenAboutMeIsNull()
    {
        var result = _validator.TestValidate(new UpdateAccountRequest(null));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ShouldPassWhenAboutMeIsWithinLimit()
    {
        var result = _validator.TestValidate(new UpdateAccountRequest("Hello world"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ShouldPassWhenAboutMeIsExactly500Chars()
    {
        var result = _validator.TestValidate(new UpdateAccountRequest(new string('a', 500)));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ShouldFailWhenAboutMeExceeds500Chars()
    {
        var result = _validator.TestValidate(new UpdateAccountRequest(new string('a', 501)));
        result.ShouldHaveValidationErrorFor(x => x.AboutMe);
    }
}
