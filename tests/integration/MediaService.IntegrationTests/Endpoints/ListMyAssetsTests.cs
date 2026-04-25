using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class ListMyAssetsTests : IClassFixture<MediaServiceFactory>
{
    private readonly MediaServiceFactory _factory;

    public ListMyAssetsTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReturnsOnlyOwnerUploadedAssets()
    {
        var ownerId = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        await CreateUploadedAssetAsync(ownerId);
        await CreateUploadedAssetAsync(ownerId);
        await CreateUploadedAssetAsync(stranger);

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.GetAsync("/api/v1/media/my");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
        body!.Items.Should().HaveCount(2);
        body.Items.Should().OnlyContain(i => i.OwnerId == ownerId);
        body.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ExcludesNonUploadedAssets()
    {
        var ownerId = Guid.NewGuid();
        await TestAssetBuilder.InitAsync(_factory, ownerId); // Initiated, not Uploaded
        await CreateUploadedAssetAsync(ownerId);

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.GetAsync("/api/v1/media/my");

        var body = await response.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
        body!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task PaginationCursorWalksFullList()
    {
        var ownerId = Guid.NewGuid();
        var assetIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            assetIds.Add(await CreateUploadedAssetAsync(ownerId));
            // tiny pause so CreatedAtUtc strictly increases between rows
            await Task.Delay(5);
        }

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var firstPage = await client.GetAsync("/api/v1/media/my?limit=2");
        var first = await firstPage.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
        first!.Items.Should().HaveCount(2);
        first.NextCursor.Should().NotBeNull();

        var secondClient = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var secondPage = await secondClient.GetAsync($"/api/v1/media/my?limit=2&cursor={first.NextCursor}");
        var second = await secondPage.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
        second!.Items.Should().HaveCount(2);

        var seen = first.Items.Concat(second.Items).Select(i => i.AssetId).ToList();
        seen.Distinct().Should().HaveCount(4, "pages must not overlap");
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/media/my");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<Guid> CreateUploadedAssetAsync(Guid ownerId)
    {
        var content = new byte[64];
        var assetId = await TestAssetBuilder.InitAndUploadAsync(_factory, ownerId, content);
        var ownerClient = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var completeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/media/upload/complete",
            new { assetId, checksum = "x" });
        completeRes.EnsureSuccessStatusCode();
        return assetId;
    }
}
