using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Infrastructure.Storage;

public sealed class MinioAvatarStorage(
    IAmazonS3 s3Client,
    IOptions<StorageOptions> options) : IAvatarStorage
{
    private const string AvatarCacheControl = "public, max-age=31536000, immutable";

    private readonly StorageOptions _options = options.Value;

    public async Task<string> UploadAsync(
        Guid userId,
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken)
    {
        var extension = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "bin",
        };

        var key = $"avatars/{userId:N}/{Guid.NewGuid():N}.{extension}";

        var putRequest = new PutObjectRequest
        {
            BucketName = _options.AvatarBucket,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType,
        };
        putRequest.Headers.CacheControl = AvatarCacheControl;

        await s3Client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);

        var publicBase = (_options.PublicEndpoint ?? _options.Endpoint).TrimEnd('/');
        return $"{publicBase}/{_options.AvatarBucket}/{key}";
    }

    public async Task<AvatarObject?> DownloadAsync(string objectUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectUrl);

        var key = GetObjectKey(objectUrl);
        if (key is null)
        {
            return null;
        }

        using var response = await s3Client.GetObjectAsync(
            new GetObjectRequest
            {
                BucketName = _options.AvatarBucket,
                Key = key,
            },
            cancellationToken).ConfigureAwait(false);

        await using var output = new MemoryStream();
        await response.ResponseStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);

        return new AvatarObject(
            output.ToArray(),
            response.Headers.ContentType ?? "application/octet-stream");
    }

    public async Task DeleteAsync(string objectUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectUrl);

        var key = GetObjectKey(objectUrl);
        if (key is null)
        {
            return;
        }

        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _options.AvatarBucket,
            Key = key,
        };

        await s3Client.DeleteObjectAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
    }

    private string? GetObjectKey(string objectUrl)
    {
        foreach (var prefix in GetObjectUrlPrefixes())
        {
            if (objectUrl.StartsWith(prefix, StringComparison.Ordinal))
            {
                return objectUrl[prefix.Length..];
            }
        }

        return null;
    }

    private IEnumerable<string> GetObjectUrlPrefixes()
    {
        var endpoints = new[] { _options.Endpoint, _options.PublicEndpoint }
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint))
            .Select(endpoint => endpoint!.TrimEnd('/'))
            .Distinct(StringComparer.Ordinal);

        foreach (var endpoint in endpoints)
        {
            yield return $"{endpoint}/{_options.AvatarBucket}/";
        }
    }
}
