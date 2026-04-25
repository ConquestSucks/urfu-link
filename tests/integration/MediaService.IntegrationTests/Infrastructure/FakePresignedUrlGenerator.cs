using MediaService.Api.Domain.Interfaces;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministic stand-in for the AWS SDK presigner; produces synthetic but
/// recognisable URLs so tests can assert on URL shape without going through MinIO.
/// </summary>
public sealed class FakePresignedUrlGenerator : IPresignedUrlGenerator
{
    public PresignedUrl ForUpload(string bucket, string objectKey, string contentType, TimeSpan ttl)
        => new($"http://test/upload/{bucket}/{objectKey}?contentType={contentType}",
               DateTimeOffset.UtcNow + ttl);

    public PresignedUrl ForDownload(string bucket, string objectKey, TimeSpan ttl)
        => new($"http://test/download/{bucket}/{objectKey}", DateTimeOffset.UtcNow + ttl);
}
