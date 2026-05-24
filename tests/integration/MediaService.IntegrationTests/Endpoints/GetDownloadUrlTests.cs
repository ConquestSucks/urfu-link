using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Grpc;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class GetDownloadUrlTests : IAsyncLifetime
{
    private readonly MediaServiceFactory _factory;

    public GetDownloadUrlTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Owner_GetsPresignedUrl()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.GetAsync($"/api/v1/media/{assetId}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DownloadUrlResponse>();
        body!.Url.Should().Contain("X-Amz-Signature");
        body.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Owner_GetsPresignedUrl_WhenGetHasJsonContentTypeWithoutBody()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/media/{assetId}/download-url")
        {
            Content = new ByteArrayContent(Array.Empty<byte>()),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonOwner_PrivateAsset_Returns403()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        var attacker = Guid.NewGuid();
        var client = TestAssetBuilder.AuthorizedClient(_factory, attacker);

        var response = await client.GetAsync($"/api/v1/media/{assetId}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonOwner_PublicAsset_GetsPresignedUrl()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId, Visibility.Public);

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

    [Fact]
    public async Task NonOwner_WithDirectGrant_GetsPresignedUrl()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);
        var beneficiary = Guid.NewGuid();
        await GrantAccessAsync(ownerId, beneficiary, assetId);

        var beneficiaryClient = TestAssetBuilder.AuthorizedClient(_factory, beneficiary);
        var response = await beneficiaryClient.GetAsync($"/api/v1/media/{assetId}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "explicit MediaAccessGrant must let a non-owner download the asset (Variant C)");
    }

    [Fact]
    public async Task NonOwner_AfterRevoke_Returns403()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);
        var beneficiary = Guid.NewGuid();
        await GrantAccessAsync(ownerId, beneficiary, assetId);
        await RevokeAccessAsync(beneficiary, assetId);

        var beneficiaryClient = TestAssetBuilder.AuthorizedClient(_factory, beneficiary);
        var response = await beneficiaryClient.GetAsync($"/api/v1/media/{assetId}/download-url");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "after revoke the previously-granted user must lose access");
    }

    private async Task GrantAccessAsync(Guid ownerId, Guid beneficiary, Guid assetId)
    {
        var (channel, http, grpc) = TestAssetBuilder.CreateGrpcClient(_factory, ownerId);
        try
        {
            var grant = new GrantAssetAccessRequest
            {
                AssetId = assetId.ToString(),
                GrantedByUserId = ownerId.ToString(),
                Source = global::MediaService.Api.Grpc.GrantSource.Direct,
            };
            grant.UserIds.Add(beneficiary.ToString());
            await grpc.GrantAssetAccessAsync(grant);
        }
        finally
        {
            channel.Dispose();
            http.Dispose();
        }
    }

    private async Task RevokeAccessAsync(Guid beneficiary, Guid assetId)
    {
        // Caller must be authenticated; the beneficiary works because gRPC
        // here only requires the JWT scheme, not asset ownership.
        var (channel, http, grpc) = TestAssetBuilder.CreateGrpcClient(_factory, beneficiary);
        try
        {
            var revoke = new RevokeAssetAccessRequest
            {
                AssetId = assetId.ToString(),
                Source = global::MediaService.Api.Grpc.GrantSource.Direct,
            };
            revoke.UserIds.Add(beneficiary.ToString());
            await grpc.RevokeAssetAccessAsync(revoke);
        }
        finally
        {
            channel.Dispose();
            http.Dispose();
        }
    }
}
