using Amazon.S3;
using Amazon.S3.Model;
using MediaService.Api.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace MediaService.Api.Infrastructure.Storage;

public sealed class PresignedUrlGenerator : IPresignedUrlGenerator, IDisposable
{
    private readonly IAmazonS3 _presignClient;
    private readonly Uri _presignEndpoint;
    private readonly bool _disposePresignClient;

    public PresignedUrlGenerator(IAmazonS3 s3Client, IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(s3Client);
        ArgumentNullException.ThrowIfNull(options);

        var presignEndpoint = ResolvePresignEndpoint(options.Value);
        _presignEndpoint = new Uri(presignEndpoint);
        _presignClient = CreatePresignClient(s3Client, options.Value, presignEndpoint, out _disposePresignClient);
    }

    public PresignedUrl ForUpload(string bucket, string objectKey, string contentType, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        var expiresAt = DateTimeOffset.UtcNow + ttl;
        var url = _presignClient.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = expiresAt.UtcDateTime,
            ContentType = contentType,
        });

        return new PresignedUrl(NormalizePresignedUrl(url), expiresAt);
    }

    public PresignedUrl ForDownload(string bucket, string objectKey, string fileName, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ttl, TimeSpan.Zero);

        var expiresAt = DateTimeOffset.UtcNow + ttl;
        var url = _presignClient.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Expires = expiresAt.UtcDateTime,
            ResponseHeaderOverrides = new ResponseHeaderOverrides
            {
                ContentDisposition = BuildAttachmentContentDisposition(fileName),
            },
        });

        return new PresignedUrl(NormalizePresignedUrl(url), expiresAt);
    }

    public void Dispose()
    {
        if (_disposePresignClient)
        {
            _presignClient.Dispose();
        }
    }

    private static IAmazonS3 CreatePresignClient(
        IAmazonS3 internalClient,
        StorageOptions options,
        string presignEndpoint,
        out bool disposePresignClient)
    {
        if (EndpointsEqual(presignEndpoint, options.Endpoint))
        {
            disposePresignClient = false;
            return internalClient;
        }

        disposePresignClient = true;
        var endpointUri = new Uri(presignEndpoint);
        var config = new AmazonS3Config
        {
            ServiceURL = presignEndpoint,
            ForcePathStyle = true,
            UseHttp = endpointUri.Scheme == Uri.UriSchemeHttp,
        };

        return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
    }

    private string NormalizePresignedUrl(string signedUrl)
    {
        if (!Uri.TryCreate(signedUrl, UriKind.Absolute, out var signedUri)
            || !string.Equals(signedUri.Host, _presignEndpoint.Host, StringComparison.OrdinalIgnoreCase)
            || signedUri.Port != _presignEndpoint.Port)
        {
            return signedUrl;
        }

        var builder = new UriBuilder(signedUri)
        {
            Scheme = _presignEndpoint.Scheme,
            Port = _presignEndpoint.IsDefaultPort ? -1 : _presignEndpoint.Port,
        };
        return builder.Uri.ToString();
    }

    private static string ResolvePresignEndpoint(StorageOptions options) =>
        string.IsNullOrWhiteSpace(options.PublicEndpoint)
            ? options.Endpoint
            : options.PublicEndpoint;

    private static string BuildAttachmentContentDisposition(string fileName)
    {
        var asciiFileName = fileName
            .Replace("\\", "_", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"attachment; filename=\"{asciiFileName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }

    private static bool EndpointsEqual(string left, string right)
    {
        if (!Uri.TryCreate(left, UriKind.Absolute, out var leftUri)
            || !Uri.TryCreate(right, UriKind.Absolute, out var rightUri))
        {
            return string.Equals(left.TrimEnd('/'), right.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(leftUri.Scheme, rightUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(leftUri.Host, rightUri.Host, StringComparison.OrdinalIgnoreCase)
            && leftUri.Port == rightUri.Port
            && string.Equals(
                leftUri.AbsolutePath.TrimEnd('/'),
                rightUri.AbsolutePath.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
    }
}
