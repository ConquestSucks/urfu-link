using FluentAssertions;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Application.Limits;
using MediaService.Api.Application.Validators;
using MediaService.Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace MediaService.UnitTests.Unit;

public class InitiateUploadValidatorTests
{
    private static InitiateUploadValidator CreateValidator() =>
        new(Options.Create(new MediaLimitsOptions()));

    [Theory]
    [InlineData("application/x-msdownload")]
    [InlineData("application/x-bat")]
    [InlineData("application/x-sh")]
    [InlineData("application/x-msi")]
    [InlineData("text/x-shellscript")]
    public void RejectsExecutableMime(string mimeType)
    {
        var sut = CreateValidator();
        var request = new InitiateUploadRequest("evil.bin", 1024, mimeType, Visibility.Private);

        var result = sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(InitiateUploadRequest.MimeType)
            && e.ErrorMessage.Contains("white-list", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AcceptsWhitelistedImage()
    {
        var sut = CreateValidator();
        var request = new InitiateUploadRequest("photo.png", 1024, "image/png", Visibility.Private);

        var result = sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
