using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using NSubstitute;
using UserService.Api.Infrastructure.Storage;

namespace UserService.UnitTests.Infrastructure;

public sealed class MinioAvatarStorageTests
{
    [Fact]
    public async Task UploadAsyncShouldStoreImmutableCacheHeader()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        PutObjectRequest? capturedRequest = null;
        s3Client
            .PutObjectAsync(
                Arg.Do<PutObjectRequest>(request => capturedRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        var storage = CreateStorage(s3Client);

        await using var content = new MemoryStream([1, 2, 3]);
        await storage.UploadAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            content,
            "image/webp",
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("public, max-age=31536000, immutable", capturedRequest.Headers.CacheControl);
        Assert.EndsWith(".webp", capturedRequest.Key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAsyncShouldDeleteObjectsByPublicEndpointUrl()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        DeleteObjectRequest? capturedRequest = null;
        s3Client
            .DeleteObjectAsync(
                Arg.Do<DeleteObjectRequest>(request => capturedRequest = request),
                Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse());

        var storage = CreateStorage(s3Client);

        await storage.DeleteAsync(
            "https://storage.ghjc.ru/user-avatars/avatars/11111111111111111111111111111111/avatar.webp",
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("user-avatars", capturedRequest.BucketName);
        Assert.Equal("avatars/11111111111111111111111111111111/avatar.webp", capturedRequest.Key);
    }

    private static MinioAvatarStorage CreateStorage(IAmazonS3 s3Client)
    {
        return new MinioAvatarStorage(
            s3Client,
            Options.Create(new StorageOptions
            {
                Endpoint = "http://minio:9000",
                PublicEndpoint = "https://storage.ghjc.ru",
                AvatarBucket = "user-avatars",
            }));
    }
}
