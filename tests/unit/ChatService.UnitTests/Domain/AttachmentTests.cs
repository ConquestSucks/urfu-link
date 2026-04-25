using FluentAssertions;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace Urfu.Link.Services.Chat.UnitTests.Domain;

public class AttachmentTests
{
    [Fact]
    public void Equality_IsValueBased()
    {
        var assetId = Guid.NewGuid();
        var thumb = Guid.NewGuid();

        var first = new Attachment(assetId, AttachmentType.Image, thumb, "photo.png", 1024, "image/png");
        var second = new Attachment(assetId, AttachmentType.Image, thumb, "photo.png", 1024, "image/png");

        second.Should().Be(first);
    }

    [Fact]
    public void Equality_DifferentAsset_NotEqual()
    {
        var first = new Attachment(Guid.NewGuid(), AttachmentType.Document, null, "doc.pdf", 100, "application/pdf");
        var second = first with { MediaAssetId = Guid.NewGuid() };

        second.Should().NotBe(first);
    }
}
