using FluentAssertions;
using MediaService.Api.Application.Access;
using MediaService.Api.Domain;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Domain.Interfaces;
using NSubstitute;

namespace MediaService.UnitTests.Unit;

public class AccessPolicyTests
{
    private readonly IMediaAccessGrantRepository _grants = Substitute.For<IMediaAccessGrantRepository>();

    private MediaAsset NewUploadedAsset(Guid ownerId, Visibility visibility = Visibility.Private)
    {
        var asset = MediaAsset.Initiate(
            Guid.NewGuid(), ownerId, visibility, AssetKind.Image,
            "media-private", "key", 100, "image/png", "f.png");
        asset.MarkUploaded();
        return asset;
    }

    [Fact]
    public async Task Owner_CanDownload_Private()
    {
        var ownerId = Guid.NewGuid();
        var policy = new AccessPolicy(_grants);
        var asset = NewUploadedAsset(ownerId);

        var ok = await policy.CanDownloadAsync(asset, ownerId, CancellationToken.None);

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task NonOwner_WithoutGrant_Forbidden_Private()
    {
        _grants.HasAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var policy = new AccessPolicy(_grants);
        var asset = NewUploadedAsset(Guid.NewGuid());

        var ok = await policy.CanDownloadAsync(asset, Guid.NewGuid(), CancellationToken.None);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task NonOwner_WithGrant_Allowed_Private()
    {
        _grants.HasAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var policy = new AccessPolicy(_grants);
        var asset = NewUploadedAsset(Guid.NewGuid());

        var ok = await policy.CanDownloadAsync(asset, Guid.NewGuid(), CancellationToken.None);

        ok.Should().BeTrue();
    }

    [Fact]
    public async Task Public_AnyAuthenticatedUser_Allowed()
    {
        var policy = new AccessPolicy(_grants);
        var asset = NewUploadedAsset(Guid.NewGuid(), Visibility.Public);

        var ok = await policy.CanDownloadAsync(asset, Guid.NewGuid(), CancellationToken.None);

        ok.Should().BeTrue();
        await _grants.DidNotReceive().HasAccessAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SoftDeletedAsset_NobodyCanDownload()
    {
        var ownerId = Guid.NewGuid();
        var policy = new AccessPolicy(_grants);
        var asset = NewUploadedAsset(ownerId);
        asset.SoftDelete();

        var ok = await policy.CanDownloadAsync(asset, ownerId, CancellationToken.None);

        ok.Should().BeFalse();
    }
}
