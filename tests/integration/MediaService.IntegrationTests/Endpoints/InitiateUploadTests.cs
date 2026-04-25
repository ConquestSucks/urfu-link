using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class InitiateUploadTests : IClassFixture<MediaServiceFactory>
{
    private readonly MediaServiceFactory _factory;

    public InitiateUploadTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    private static ClaimsPrincipal MakeUser(Guid userId)
        => new(new ClaimsIdentity([new Claim("sub", userId.ToString())], TestAuthHandler.SchemeName));

    private static HttpClient AuthorizedClient(MediaServiceFactory factory, Guid userId)
    {
        TestAuthHandler.CurrentPrincipal = MakeUser(userId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return client;
    }

    [Fact]
    public async Task ValidImage_ReturnsPresignedUrlAndAssetId()
    {
        var client = AuthorizedClient(_factory, Guid.NewGuid());
        var req = new InitiateUploadRequest("photo.png", 1024, "image/png", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadInitResponse>();
        body.Should().NotBeNull();
        body!.AssetId.Should().NotBeEmpty();
        body.PresignedPutUrl.Should().StartWith("http://test/upload/media-private/");
        body.Bucket.Should().Be("media-private");
        body.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task PublicVisibility_RoutesToPublicBucket()
    {
        var client = AuthorizedClient(_factory, Guid.NewGuid());
        var req = new InitiateUploadRequest("avatar.png", 1024, "image/png", Visibility.Public);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadInitResponse>();
        body!.Bucket.Should().Be("media-public");
    }

    [Fact]
    public async Task Image_OverSizeLimit_Returns400()
    {
        var client = AuthorizedClient(_factory, Guid.NewGuid());
        var req = new InitiateUploadRequest("huge.png", 50L * 1024 * 1024, "image/png", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExecutableMime_Rejected()
    {
        var client = AuthorizedClient(_factory, Guid.NewGuid());
        var req = new InitiateUploadRequest("evil.exe", 1024, "application/x-msdownload", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MissingIdempotencyKey_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = MakeUser(Guid.NewGuid());
        var client = _factory.CreateClient();
        var req = new InitiateUploadRequest("a.png", 1024, "image/png", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var req = new InitiateUploadRequest("a.png", 1024, "image/png", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
