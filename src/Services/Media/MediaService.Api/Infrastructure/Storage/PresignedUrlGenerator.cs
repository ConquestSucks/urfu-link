using Amazon.S3;
using Amazon.S3.Model;
using MediaService.Api.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace MediaService.Api.Infrastructure.Storage;

public sealed class PresignedUrlGenerator(
    IAmazonS3 s3Client,
    IOptions<StorageOptions> options) : IPresignedUrlGenerator
{
    private readonly StorageOptions _options = options.Value;

    public PresignedUrl ForUpload(string bucket, string objectKey, string contentType, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        var expiresAt = DateTimeOffset.UtcNow + ttl;
        var url = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType,
        });

        return new PresignedUrl(RewriteForBrowser(url), expiresAt);
    }

    public PresignedUrl ForDownload(string bucket, string objectKey, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        var expiresAt = DateTimeOffset.UtcNow + ttl;
        var url = s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
        });

        return new PresignedUrl(RewriteForBrowser(url), expiresAt);
    }

    /// <summary>
    /// In dev MinIO is reached as <c>http://minio:9000</c> from inside the cluster
    /// but as <c>http://localhost:9000</c> from the developer's browser. The S3
    /// client always signs with the cluster endpoint, so we rewrite the host part
    /// of the returned URL for clients living outside the cluster.
    /// </summary>
    private string RewriteForBrowser(string signedUrl)
    {
        if (string.IsNullOrEmpty(_options.PublicEndpoint)
            || string.Equals(_options.PublicEndpoint, _options.Endpoint, StringComparison.OrdinalIgnoreCase))
        {
            return signedUrl;
        }

        var internalBase = _options.Endpoint.TrimEnd('/');
        var publicBase = _options.PublicEndpoint.TrimEnd('/');
        return signedUrl.StartsWith(internalBase, StringComparison.Ordinal)
            ? string.Concat(publicBase, signedUrl.AsSpan(internalBase.Length))
            : signedUrl;
    }
}
