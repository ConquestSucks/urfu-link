using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Enums;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// Test helpers for creating <c>MediaAsset</c> rows and (optionally) the
/// matching MinIO object via the real production endpoints.
/// </summary>
public static class TestAssetBuilder
{
    public static ClaimsPrincipal MakeUser(Guid userId)
        => new(new ClaimsIdentity([new Claim("sub", userId.ToString())], TestAuthHandler.SchemeName));

    public static HttpClient AuthorizedClient(MediaServiceFactory factory, Guid userId)
    {
        TestAuthHandler.CurrentPrincipal = MakeUser(userId);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return client;
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
        int sizeBytes = 64)
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
