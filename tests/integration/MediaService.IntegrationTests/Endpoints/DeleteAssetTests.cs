using System.Net;
using FluentAssertions;
using MediaService.Api.Domain.Events;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class DeleteAssetTests : IClassFixture<MediaServiceFactory>
{
    private readonly MediaServiceFactory _factory;

    public DeleteAssetTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Owner_SoftDeletesAsset_AndPublishesEvent()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);
        _factory.OutboxWriter.Clear();

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.DeleteAsync($"/api/v1/media/{assetId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Subsequent metadata read should now 404 (asset.IsAccessible == false).
        var followUp = await client.GetAsync($"/api/v1/media/{assetId}/metadata");
        followUp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<MediaAssetDeletedEvent>()
            .Should().Contain(ev => ev.AssetId == assetId,
                "soft delete must enqueue MediaAssetDeletedEvent so downstream consumers can purge attachments");
    }

    [Fact]
    public async Task NonOwner_Returns403()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var attacker = Guid.NewGuid();
        var client = TestAssetBuilder.AuthorizedClient(_factory, attacker);
        var response = await client.DeleteAsync($"/api/v1/media/{assetId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UnknownAssetId_Returns404()
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, Guid.NewGuid());

        var response = await client.DeleteAsync($"/api/v1/media/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsset_MissingIdempotencyKey_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestAssetBuilder.MakeUser(Guid.NewGuid());
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/media/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
