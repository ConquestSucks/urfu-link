using FluentAssertions;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Media;
using Urfu.Link.Services.Notification.Application.Handlers.Media;
using Urfu.Link.Services.Notification.Domain.Enums;

namespace NotificationService.UnitTests.Application;

public sealed class MediaAssetUploadedHandlerTests
{
    [Fact]
    public async Task PrepareAsync_UsesOriginalFileNameAsNotificationBody()
    {
        var ownerId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var evt = new MediaAssetUploadedEvent(
            assetId,
            ownerId,
            MediaVisibility.Private,
            MediaAssetKind.Image,
            "media",
            "uploads/photo.png",
            1024,
            "image/png",
            "photo.png");

        var intents = await new MediaAssetUploadedHandler().PrepareAsync(evt, default);

        var intent = intents.Single();
        intent.RecipientUserId.Should().Be(ownerId);
        intent.Category.Should().Be(NotificationCategory.MediaUploadProcessed);
        intent.Content.Body.Should().Be("photo.png");
        intent.Data.Values["fileName"].Should().Be("photo.png");
    }

    [Fact]
    public async Task PrepareAsync_UsesObjectKeyFileNameWhenLegacyPayloadHasNoOriginalFileName()
    {
        var ownerId = Guid.NewGuid();
        var assetId = Guid.NewGuid();
        var evt = new MediaAssetUploadedEvent(
            assetId,
            ownerId,
            MediaVisibility.Private,
            MediaAssetKind.Image,
            "media",
            "uploads/photo.png",
            1024,
            "image/png",
            null!);

        var intents = await new MediaAssetUploadedHandler().PrepareAsync(evt, default);

        var intent = intents.Single();
        intent.Content.Body.Should().Be("photo.png");
        intent.Data.Values["fileName"].Should().Be("photo.png");
    }

    [Fact]
    public async Task PrepareAsync_SkipsPayloadWithoutOwner()
    {
        var evt = new MediaAssetUploadedEvent(
            Guid.NewGuid(),
            Guid.Empty,
            MediaVisibility.Private,
            MediaAssetKind.Image,
            "media",
            "uploads/photo.png",
            1024,
            "image/png",
            null!);

        var intents = await new MediaAssetUploadedHandler().PrepareAsync(evt, default);

        intents.Should().BeEmpty();
    }
}
