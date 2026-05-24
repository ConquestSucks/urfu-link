using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using MediaService.Api.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace MediaService.UnitTests.Unit;

public class PresignedUrlGeneratorTests
{
    [Fact]
    public void ForUpload_SignsAgainstPublicEndpointWhenConfigured()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var options = Options.Create(new StorageOptions
        {
            Endpoint = "http://minio:9000",
            PublicEndpoint = "http://localhost:9000",
            AccessKey = "minio",
            SecretKey = "minio123",
            PrivateBucket = "media-private",
            PublicBucket = "media-public",
        });
        using var sut = new PresignedUrlGenerator(s3, options);

        var result = sut.ForUpload(
            "media-private",
            "owner/file.json",
            "application/json",
            TimeSpan.FromMinutes(15));

        result.Url.Should().StartWith("http://localhost:9000/media-private/owner/file.json?");
        result.Url.Should().Contain("X-Amz-Signature=");
        s3.DidNotReceive().GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>());
    }

    [Fact]
    public void ForDownload_RequestsAttachmentContentDisposition()
    {
        var s3 = Substitute.For<IAmazonS3>();
        s3.GetPreSignedURL(Arg.Any<GetPreSignedUrlRequest>())
            .Returns("http://localhost:9000/media-private/owner/file.json?X-Amz-Signature=abc");
        var options = Options.Create(new StorageOptions
        {
            Endpoint = "http://localhost:9000",
            AccessKey = "minio",
            SecretKey = "minio123",
            PrivateBucket = "media-private",
            PublicBucket = "media-public",
        });
        using var sut = new PresignedUrlGenerator(s3, options);

        sut.ForDownload(
            "media-private",
            "owner/file.json",
            "file.json",
            TimeSpan.FromMinutes(15));

        s3.Received().GetPreSignedURL(Arg.Is<GetPreSignedUrlRequest>(request =>
            request.ResponseHeaderOverrides != null &&
            request.ResponseHeaderOverrides.ContentDisposition == "attachment; filename=\"file.json\"; filename*=UTF-8''file.json"));
    }
}
