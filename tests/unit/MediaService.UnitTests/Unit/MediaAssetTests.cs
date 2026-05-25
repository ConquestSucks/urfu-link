using FluentAssertions;
using MediaService.Api.Domain;
using MediaService.Api.Domain.Enums;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;

namespace MediaService.UnitTests.Unit;

public class MediaAssetTests
{
    private static MediaAsset NewInitiated(Guid? assetId = null, Guid? ownerId = null)
        => MediaAsset.Initiate(
            id: assetId ?? Guid.NewGuid(),
            ownerId: ownerId ?? Guid.NewGuid(),
            visibility: Visibility.Private,
            kind: AssetKind.Image,
            bucket: "media-private",
            objectKey: "owner/asset/file.png",
            size: 1024,
            mimeType: "image/png",
            originalFileName: "file.png");

    [Fact]
    public void Initiate_SetsInitiatedState_AndDoesNotEmitEvent()
    {
        var asset = NewInitiated();

        asset.State.Should().Be(AssetState.Initiated);
        asset.DomainEvents.Should().BeEmpty();
        asset.IsAccessible.Should().BeFalse();
    }

    [Fact]
    public void Initiate_RejectsNonPositiveSize()
    {
        var act = () => MediaAsset.Initiate(
            Guid.NewGuid(), Guid.NewGuid(), Visibility.Private, AssetKind.Document,
            "media-private", "key", size: 0, "application/pdf", "f.pdf");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void MarkUploaded_TransitionsToUploaded_AndEmitsUploadedEvent()
    {
        var asset = NewInitiated();

        asset.MarkUploaded(checksum: "abc");

        asset.State.Should().Be(AssetState.Uploaded);
        asset.UploadedAtUtc.Should().NotBeNull();
        asset.Checksum.Should().Be("abc");
        asset.IsAccessible.Should().BeTrue();
        asset.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<MediaAssetUploadedEvent>();
    }

    [Fact]
    public void MarkUploaded_FromOtherState_Throws()
    {
        var asset = NewInitiated();
        asset.MarkUploaded();

        var act = () => asset.MarkUploaded();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SoftDelete_FromUploaded_TransitionsAndEmitsEvent()
    {
        var asset = NewInitiated();
        asset.MarkUploaded();
        asset.ClearDomainEvents();

        asset.SoftDelete();

        asset.State.Should().Be(AssetState.Deleted);
        asset.DeletedAtUtc.Should().NotBeNull();
        asset.IsAccessible.Should().BeFalse();
        asset.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<MediaAssetDeletedEvent>();
    }

    [Fact]
    public void SoftDelete_FromInitiated_Throws()
    {
        var asset = NewInitiated();

        var act = () => asset.SoftDelete();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HardDelete_FromDeleted_TransitionsAndEmitsEvent()
    {
        var asset = NewInitiated();
        asset.MarkUploaded();
        asset.SoftDelete();
        asset.ClearDomainEvents();

        asset.HardDelete();

        asset.State.Should().Be(AssetState.HardDeleted);
        asset.HardDeletedAtUtc.Should().NotBeNull();
        asset.IsAccessible.Should().BeFalse();
        asset.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<MediaAssetHardDeletedEvent>();
    }

    [Fact]
    public void HardDelete_BeforeSoftDelete_Throws()
    {
        var asset = NewInitiated();
        asset.MarkUploaded();

        var act = () => asset.HardDelete();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailed_FromInitiated_TransitionsToFailed()
    {
        var asset = NewInitiated();

        asset.MarkFailed();

        asset.State.Should().Be(AssetState.Failed);
        asset.IsAccessible.Should().BeFalse();
    }
}
