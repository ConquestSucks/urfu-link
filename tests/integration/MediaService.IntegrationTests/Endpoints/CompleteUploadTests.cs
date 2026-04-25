using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Domain.Events;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class CompleteUploadTests : IAsyncLifetime
{
    private const int SamplePayloadSize = 2048;
    private readonly MediaServiceFactory _factory;

    public CompleteUploadTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CompleteUpload_AfterPut_ReturnsNoContent_AndPublishesEvent()
    {
        _factory.ResetCapturedState();
        var ownerId = Guid.NewGuid();
        var content = Enumerable.Range(0, SamplePayloadSize).Select(i => (byte)(i & 0xFF)).ToArray();

        var assetId = await TestAssetBuilder.InitAndUploadAsync(_factory, ownerId, content);

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var req = new CompleteUploadRequest(assetId, "test-checksum");

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/complete", req);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<MediaAssetUploadedEvent>()
            .Should().Contain(ev => ev.AssetId == assetId,
                "complete must enqueue MediaAssetUploadedEvent for downstream consumers (ChatService etc.)");
    }

    [Fact]
    public async Task CompleteUpload_WrongOwner_Returns403()
    {
        var ownerId = Guid.NewGuid();
        var init = await TestAssetBuilder.InitAsync(_factory, ownerId);

        var attacker = Guid.NewGuid();
        var client = TestAssetBuilder.AuthorizedClient(_factory, attacker);
        var req = new CompleteUploadRequest(init.AssetId, "checksum");

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/complete", req);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompleteUpload_ObjectNotInStorage_Returns400()
    {
        var ownerId = Guid.NewGuid();
        var init = await TestAssetBuilder.InitAsync(_factory, ownerId);

        // Skip the PUT — call complete on an asset that has no bytes uploaded yet.
        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var req = new CompleteUploadRequest(init.AssetId, "checksum");

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/complete", req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CompleteUpload_AssetNotFound_Returns404()
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, Guid.NewGuid());
        var req = new CompleteUploadRequest(Guid.NewGuid(), "checksum");

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/complete", req);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompleteUpload_MissingIdempotencyKey_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestAssetBuilder.MakeUser(Guid.NewGuid());
        var client = _factory.CreateClient();
        var req = new CompleteUploadRequest(Guid.NewGuid(), "checksum");

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/complete", req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
