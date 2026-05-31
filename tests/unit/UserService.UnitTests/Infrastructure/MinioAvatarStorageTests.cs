using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using NSubstitute;
using UserService.Api.Domain.Interfaces;
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

        await storage.UploadAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreateProcessedAvatar(),
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal("public, max-age=31536000, immutable", capturedRequest.Headers.CacheControl);
        Assert.EndsWith("-256.webp", capturedRequest.Key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsyncShouldStoreAllAvatarVariants()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var capturedRequests = new List<PutObjectRequest>();
        s3Client
            .PutObjectAsync(
                Arg.Do<PutObjectRequest>(request => capturedRequests.Add(request)),
                Arg.Any<CancellationToken>())
            .Returns(new PutObjectResponse());

        var storage = CreateStorage(s3Client);

        var avatarUrl = await storage.UploadAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CreateProcessedAvatar(),
            CancellationToken.None);

        Assert.Equal(3, capturedRequests.Count);
        Assert.Contains(capturedRequests, request => request.Key.EndsWith("-64.webp", StringComparison.Ordinal));
        Assert.Contains(capturedRequests, request => request.Key.EndsWith("-128.webp", StringComparison.Ordinal));
        Assert.Contains(capturedRequests, request => request.Key.EndsWith("-256.webp", StringComparison.Ordinal));
        Assert.EndsWith("-256.webp", avatarUrl, StringComparison.Ordinal);
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

    [Fact]
    public async Task DeleteAsyncShouldDeleteAllVariantSiblings()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        var capturedRequests = new List<DeleteObjectRequest>();
        s3Client
            .DeleteObjectAsync(
                Arg.Do<DeleteObjectRequest>(request => capturedRequests.Add(request)),
                Arg.Any<CancellationToken>())
            .Returns(new DeleteObjectResponse());

        var storage = CreateStorage(s3Client);

        await storage.DeleteAsync(
            "https://storage.ghjc.ru/user-avatars/avatars/11111111111111111111111111111111/avatar-256.webp",
            CancellationToken.None);

        Assert.Equal(
            [
                "avatars/11111111111111111111111111111111/avatar-64.webp",
                "avatars/11111111111111111111111111111111/avatar-128.webp",
                "avatars/11111111111111111111111111111111/avatar-256.webp",
            ],
            capturedRequests.Select(request => request.Key).ToArray());
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

    private static ProcessedAvatar CreateProcessedAvatar()
    {
        return new ProcessedAvatar([
            new ProcessedAvatarVariant(64, new byte[] { 1 }, "image/webp"),
            new ProcessedAvatarVariant(128, new byte[] { 2 }, "image/webp"),
            new ProcessedAvatarVariant(256, new byte[] { 3 }, "image/webp"),
        ]);
    }
}
