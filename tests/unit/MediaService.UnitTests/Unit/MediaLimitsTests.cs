using FluentAssertions;
using MediaService.Api.Application.Limits;
using MediaService.Api.Domain.Enums;

namespace MediaService.UnitTests.Unit;

public class MediaLimitsTests
{
    [Theory]
    [InlineData("image/jpeg", AssetKind.Image)]
    [InlineData("image/png", AssetKind.Image)]
    [InlineData("image/webp", AssetKind.Image)]
    [InlineData("video/mp4", AssetKind.Video)]
    [InlineData("audio/ogg", AssetKind.Voice)]
    [InlineData("audio/opus", AssetKind.Voice)]
    [InlineData("audio/mpeg", AssetKind.Audio)]
    [InlineData("application/pdf", AssetKind.Document)]
    [InlineData("text/plain", AssetKind.Document)]
    public void Whitelisted_MimeTypes_ResolveToExpectedKind(string mime, AssetKind expected)
    {
        var ok = MimeTypeCatalog.TryResolve(mime, out var kind);
        ok.Should().BeTrue();
        kind.Should().Be(expected);
    }

    [Theory]
    [InlineData("application/x-msdownload")] // .exe
    [InlineData("application/x-bat")]
    [InlineData("application/x-sh")]
    [InlineData("application/octet-stream")]
    [InlineData("foo/bar")]
    public void NonWhitelisted_MimeTypes_AreRejected(string mime)
    {
        var ok = MimeTypeCatalog.TryResolve(mime, out _);
        ok.Should().BeFalse();
    }

    [Fact]
    public void DefaultLimits_AreReasonable()
    {
        var limits = new MediaLimitsOptions();
        limits.Image.MaxSizeBytes.Should().Be(20L * 1024 * 1024);
        limits.Video.MaxSizeBytes.Should().Be(100L * 1024 * 1024);
        limits.Voice.MaxSizeBytes.Should().Be(10L * 1024 * 1024);
        limits.Audio.MaxSizeBytes.Should().Be(50L * 1024 * 1024);
        limits.Document.MaxSizeBytes.Should().Be(100L * 1024 * 1024);
    }

    [Fact]
    public void For_ReturnsKindLimit()
    {
        var limits = new MediaLimitsOptions();
        limits.For(AssetKind.Image).Should().Be(limits.Image);
        limits.For(AssetKind.Video).Should().Be(limits.Video);
    }
}
