using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class GetMetadataTests : IClassFixture<MediaServiceFactory>
{
    private readonly MediaServiceFactory _factory;

    public GetMetadataTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Owner_GetsMetadata()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.GetAsync($"/api/v1/media/{assetId}/metadata");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AssetMetadataResponse>();
        body!.AssetId.Should().Be(assetId);
        body.OwnerId.Should().Be(ownerId);
        body.MimeType.Should().Be("image/png");
        body.State.Should().Be(AssetState.Uploaded);
    }

    [Fact]
    public async Task NonOwner_PrivateAsset_Returns403()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var attacker = Guid.NewGuid();
        var client = TestAssetBuilder.AuthorizedClient(_factory, attacker);

        var response = await client.GetAsync($"/api/v1/media/{assetId}/metadata");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownAssetId_Returns404()
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, Guid.NewGuid());

        var response = await client.GetAsync($"/api/v1/media/{Guid.NewGuid()}/metadata");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
