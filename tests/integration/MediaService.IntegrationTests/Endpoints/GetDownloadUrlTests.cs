using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class GetDownloadUrlTests : IClassFixture<MediaServiceFactory>
{
    private readonly MediaServiceFactory _factory;

    public GetDownloadUrlTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Owner_GetsPresignedUrl()
    {
        var ownerId = Guid.NewGuid();
        var content = new byte[256];
        var assetId = await TestAssetBuilder.InitAndUploadAsync(_factory, ownerId, content);

        // Move the asset to Uploaded so AccessPolicy.IsAccessible passes.
        var ownerClient = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        await ownerClient.PostAsJsonAsync("/api/v1/media/upload/complete",
            new { assetId, checksum = "x" });

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.GetAsync($"/api/v1/media/{assetId}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DownloadUrlResponse>();
        body!.Url.Should().Contain("X-Amz-Signature");
        body.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task NonOwner_PrivateAsset_Returns403()
    {
        var ownerId = Guid.NewGuid();
        var content = new byte[256];
        var assetId = await TestAssetBuilder.InitAndUploadAsync(_factory, ownerId, content);

        var ownerClient = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        await ownerClient.PostAsJsonAsync("/api/v1/media/upload/complete",
            new { assetId, checksum = "x" });

        var attacker = Guid.NewGuid();
        var client = TestAssetBuilder.AuthorizedClient(_factory, attacker);

        var response = await client.GetAsync($"/api/v1/media/{assetId}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonOwner_PublicAsset_GetsPresignedUrl()
    {
        var ownerId = Guid.NewGuid();
        var content = new byte[256];
        var assetId = await TestAssetBuilder.InitAndUploadAsync(_factory, ownerId, content,
            visibility: Visibility.Public);

        var ownerClient = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        await ownerClient.PostAsJsonAsync("/api/v1/media/upload/complete",
            new { assetId, checksum = "x" });

        var stranger = Guid.NewGuid();
        var client = TestAssetBuilder.AuthorizedClient(_factory, stranger);

        var response = await client.GetAsync($"/api/v1/media/{assetId}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnknownAssetId_Returns404()
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, Guid.NewGuid());

        var response = await client.GetAsync($"/api/v1/media/{Guid.NewGuid()}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
