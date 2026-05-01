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

    [Fact]
    public void RejectsOverLongFileName()
    {
        var sut = CreateValidator();
        var longName = new string('a', 201) + ".png";
        var request = new InitiateUploadRequest(longName, 1024, "image/png", Visibility.Private);

        var result = sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InitiateUploadRequest.FileName));
    }

    [Fact]
    public void RejectsOverLongMimeType()
    {
        var sut = CreateValidator();
        var longMime = "image/" + new string('x', 200);
        var request = new InitiateUploadRequest("photo.png", 1024, longMime, Visibility.Private);

        var result = sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InitiateUploadRequest.MimeType));
    }

    [Fact]
    public void RejectsVoiceUploadWithoutDeclaredDuration()
    {
        // EPIC #206 caps voice at 5 minutes. The size cap alone allows a 30-minute
        // high-bitrate recording to slip through, so DurationSeconds is mandatory.
        var sut = CreateValidator();
        var request = new InitiateUploadRequest("note.ogg", 1024, "audio/ogg", Visibility.Private);

        var result = sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(InitiateUploadRequest.DurationSeconds)
            && e.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RejectsVoiceUploadExceedingMaxDuration()
    {
        var sut = CreateValidator();
        var request = new InitiateUploadRequest(
            "note.ogg", 1024, "audio/ogg", Visibility.Private, DurationSeconds: 301);

        var result = sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(InitiateUploadRequest.DurationSeconds)
            && e.ErrorMessage.Contains("exceeds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RejectsVoiceUploadWithNonPositiveDuration()
    {
        var sut = CreateValidator();
        var request = new InitiateUploadRequest(
            "note.ogg", 1024, "audio/ogg", Visibility.Private, DurationSeconds: 0);

        var result = sut.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(InitiateUploadRequest.DurationSeconds));
    }

    [Fact]
    public void AcceptsVoiceUploadAtMaxDurationBoundary()
    {
        var sut = CreateValidator();
        var request = new InitiateUploadRequest(
            "note.ogg", 1024, "audio/ogg", Visibility.Private, DurationSeconds: 300);

        var result = sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IgnoresDurationForKindsWithoutMaxDuration()
    {
        // Image / document have no MaxDurationSeconds — the field is optional and ignored
        // even if a malformed client sends a value.
        var sut = CreateValidator();
        var request = new InitiateUploadRequest(
            "photo.png", 1024, "image/png", Visibility.Private, DurationSeconds: 9999);

        var result = sut.Validate(request);

        result.IsValid.Should().BeTrue();
    }
}
