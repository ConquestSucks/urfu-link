using System.Text.Json;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;

namespace MediaService.UnitTests.Unit;

public class AssetMetadataResponseSerializationTests
{
    [Fact]
    public void StateSerializesAsStableNumericValue()
    {
        var response = new AssetMetadataResponse(
            AssetId: Guid.NewGuid(),
            OwnerId: Guid.NewGuid(),
            Visibility: Visibility.Private,
            Kind: AssetKind.Image,
            Size: 1024,
            MimeType: "image/png",
            OriginalFileName: "photo.png",
            State: AssetState.Uploaded,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UploadedAtUtc: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(response);

        json.Should().Contain("\"State\":1",
            "AssetState.Uploaded must serialise as the explicit numeric value 1 so renaming the enum cannot break wire clients");
    }
}
