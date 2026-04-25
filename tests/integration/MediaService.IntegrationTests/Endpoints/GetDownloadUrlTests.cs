using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using global::Grpc.Net.Client;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Grpc;
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

    [Fact]
    public async Task NonOwner_WithDirectGrant_GetsPresignedUrl()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.InitAndUploadAsync(_factory, ownerId, new byte[64]);

        var ownerClient = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        await ownerClient.PostAsJsonAsync("/api/v1/media/upload/complete",
            new { assetId, checksum = "x" });

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
        var assetId = await TestAssetBuilder.InitAndUploadAsync(_factory, ownerId, new byte[64]);

        var ownerClient = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        await ownerClient.PostAsJsonAsync("/api/v1/media/upload/complete",
            new { assetId, checksum = "x" });

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
        TestAuthHandler.CurrentPrincipal = TestAssetBuilder.MakeUser(ownerId);
        using var http = _factory.CreateClient();
        using var channel = GrpcChannel.ForAddress(http.BaseAddress!,
            new GrpcChannelOptions { HttpClient = http });
        var grpc = new InternalApi.InternalApiClient(channel);
        var grant = new GrantAssetAccessRequest
        {
            AssetId = assetId.ToString(),
            GrantedByUserId = ownerId.ToString(),
            Source = global::MediaService.Api.Grpc.GrantSource.Direct,
        };
        grant.UserIds.Add(beneficiary.ToString());
        await grpc.GrantAssetAccessAsync(grant);
    }

    private async Task RevokeAccessAsync(Guid beneficiary, Guid assetId)
    {
        // Caller must be authenticated; reuse the beneficiary principal — gRPC
        // here only requires the JWT scheme, not asset ownership.
        TestAuthHandler.CurrentPrincipal = TestAssetBuilder.MakeUser(beneficiary);
        using var http = _factory.CreateClient();
        using var channel = GrpcChannel.ForAddress(http.BaseAddress!,
            new GrpcChannelOptions { HttpClient = http });
        var grpc = new InternalApi.InternalApiClient(channel);
        var revoke = new RevokeAssetAccessRequest
        {
            AssetId = assetId.ToString(),
            Source = global::MediaService.Api.Grpc.GrantSource.Direct,
        };
        revoke.UserIds.Add(beneficiary.ToString());
        await grpc.RevokeAssetAccessAsync(revoke);
    }
}
