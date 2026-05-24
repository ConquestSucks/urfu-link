using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using global::Grpc.Net.Client;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Grpc;
using MediaService.Api.Infrastructure.Auth;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// Test helpers for creating <c>MediaAsset</c> rows and (optionally) the
/// matching MinIO object via the real production endpoints.
/// </summary>
public static class TestAssetBuilder
{
    /// <summary>Default in-memory payload used by helpers that just need "some bytes".</summary>
    public const int DefaultContentSize = 64;

    public static ClaimsPrincipal MakeUser(Guid userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
        };
        foreach (var role in roles ?? [])
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("groups", role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, TestAuthHandler.SchemeName));
    }

    public static HttpClient AuthorizedClient(MediaServiceFactory factory, Guid userId)
    {
        TestAuthHandler.CurrentPrincipal = MakeUser(userId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return client;
    }

    /// <summary>
    /// Builds an <see cref="InternalApi.InternalApiClient"/> backed by the test
    /// server, with <see cref="TestAuthHandler.CurrentPrincipal"/> set to the
    /// supplied user. The caller owns disposal of the returned channel + client.
    /// </summary>
    public static (GrpcChannel Channel, HttpClient Http, InternalApi.InternalApiClient Client) CreateGrpcClient(
        MediaServiceFactory factory, Guid userId)
    {
        TestAuthHandler.CurrentPrincipal = MakeUser(userId, InternalGrpcAuthorizationPolicy.MediaInternalRole);
        var http = factory.CreateClient();
        var channel = GrpcChannel.ForAddress(http.BaseAddress!,
            new GrpcChannelOptions { HttpClient = http });
        return (channel, http, new InternalApi.InternalApiClient(channel));
    }

    public static async Task<UploadInitResponse> InitAsync(
        MediaServiceFactory factory,
        Guid ownerId,
        long size = 1024,
        Visibility visibility = Visibility.Private,
        string mimeType = "image/png",
        string fileName = "photo.png")
    {
        var client = AuthorizedClient(factory, ownerId);
        var req = new InitiateUploadRequest(fileName, size, mimeType, visibility);
        var response = await client.PostAsJsonAsync("/api/v1/media/upload/init", req);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<UploadInitResponse>();
        return body!;
    }

    public static async Task PutAsync(string presignedUrl, byte[] content, string mimeType)
    {
        // AWSSDK presigning forces https:// even when ServiceURL is plain http,
        // and Testcontainers MinIO does not terminate TLS. Downgrade for tests
        // only -- production talks to a real MinIO/S3 over HTTPS.
        var uri = new Uri(presignedUrl);
        if (uri.Scheme == Uri.UriSchemeHttps && uri.Host is "127.0.0.1" or "localhost")
        {
            uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttp, Port = uri.Port }.Uri;
        }

        using var http = new HttpClient();
        using var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        var response = await http.PutAsync(uri, body);
        response.EnsureSuccessStatusCode();
    }

    public static async Task<Guid> InitAndUploadAsync(
        MediaServiceFactory factory,
        Guid ownerId,
        byte[] content,
        Visibility visibility = Visibility.Private,
        string mimeType = "image/png",
        string fileName = "photo.png")
    {
        var init = await InitAsync(factory, ownerId, content.Length, visibility, mimeType, fileName);
        await PutAsync(init.PresignedPutUrl, content, mimeType);
        return init.AssetId;
    }

    /// <summary>
    /// Init + PUT + Complete cycle for tests that need an asset already in the
    /// Uploaded state (e.g. download / metadata / delete / list).
    /// </summary>
    public static async Task<Guid> CreateUploadedAssetAsync(
        MediaServiceFactory factory,
        Guid ownerId,
        Visibility visibility = Visibility.Private,
        int sizeBytes = DefaultContentSize)
    {
        var content = new byte[sizeBytes];
        var assetId = await InitAndUploadAsync(factory, ownerId, content, visibility);
        var ownerClient = AuthorizedClient(factory, ownerId);
        var completeRes = await ownerClient.PostAsJsonAsync(
            "/api/v1/media/upload/complete",
            new CompleteUploadRequest(assetId, "x"));
        completeRes.EnsureSuccessStatusCode();
        return assetId;
    }
}
