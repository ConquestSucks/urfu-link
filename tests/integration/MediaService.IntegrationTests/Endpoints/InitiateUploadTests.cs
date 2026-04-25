using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class InitiateUploadTests : IClassFixture<MediaServiceFactory>
{
    private const long SmallImageSize = 1024;
    private const long ImageSizeLimitExceeded = 50L * 1024 * 1024;
    private readonly MediaServiceFactory _factory;

    public InitiateUploadTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ValidImage_ReturnsPresignedUrlAndAssetId()
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, Guid.NewGuid());
        var req = new InitiateUploadRequest("photo.png", SmallImageSize, "image/png", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadInitResponse>();
        body.Should().NotBeNull();
        body!.AssetId.Should().NotBeEmpty();
        var presignedUrl = new Uri(body.PresignedPutUrl);
        presignedUrl.Authority.Should().Be(new Uri(_factory.MinioEndpoint).Authority,
            "presigned URL must point at the MinIO container host:port");
        body.PresignedPutUrl.Should().Contain("/media-private/").And.Contain("X-Amz-Signature");
        body.Bucket.Should().Be("media-private");
        body.ExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task PublicVisibility_RoutesToPublicBucket()
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, Guid.NewGuid());
        var req = new InitiateUploadRequest("avatar.png", SmallImageSize, "image/png", Visibility.Public);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadInitResponse>();
        body!.Bucket.Should().Be("media-public");
    }

    [Fact]
    public async Task Image_OverSizeLimit_Returns400()
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, Guid.NewGuid());
        var req = new InitiateUploadRequest("huge.png", ImageSizeLimitExceeded, "image/png", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("evil.exe", "application/x-msdownload")]
    [InlineData("evil.bat", "application/x-bat")]
    [InlineData("install.sh", "application/x-sh")]
    [InlineData("script.ps1", "application/x-powershell")]
    [InlineData("setup.msi", "application/x-msi")]
    [InlineData("launcher.com", "application/x-msdos-program")]
    [InlineData("payload.bin", "application/octet-stream")]
    public async Task ForbiddenMime_Returns400(string fileName, string mimeType)
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, Guid.NewGuid());
        var req = new InitiateUploadRequest(fileName, SmallImageSize, mimeType, Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MissingIdempotencyKey_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = TestAssetBuilder.MakeUser(Guid.NewGuid());
        var client = _factory.CreateClient();
        var req = new InitiateUploadRequest("a.png", SmallImageSize, "image/png", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var req = new InitiateUploadRequest("a.png", SmallImageSize, "image/png", Visibility.Private);

        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
