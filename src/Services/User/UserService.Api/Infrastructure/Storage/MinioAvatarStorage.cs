using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UserService.Api.Domain;
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
        ProcessedAvatar avatar,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(avatar);

        var avatarId = Guid.NewGuid().ToString("N");
        string? canonicalKey = null;
        foreach (var variant in avatar.Variants)
        {
            var extension = GetExtension(variant.ContentType);
            var key = $"avatars/{userId:N}/{avatarId}-{AvatarVariantNaming.CurrentVersion}-{variant.Size}.{extension}";
            await using var fileStream = new MemoryStream(variant.Content.ToArray(), writable: false);
            var putRequest = new PutObjectRequest
            {
                BucketName = _options.AvatarBucket,
                Key = key,
                InputStream = fileStream,
                ContentType = variant.ContentType,
            };
            putRequest.Headers.CacheControl = AvatarCacheControl;

            await s3Client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);

            if (variant.Size == avatar.CanonicalVariant.Size)
            {
                canonicalKey = key;
            }
        }

        var publicBase = (_options.PublicEndpoint ?? _options.Endpoint).TrimEnd('/');
        return $"{publicBase}/{_options.AvatarBucket}/{canonicalKey ?? throw new InvalidOperationException("No avatar variants were uploaded.")}";
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

        foreach (var keyToDelete in GetObjectKeysToDelete(key))
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _options.AvatarBucket,
                Key = keyToDelete,
            };

            await s3Client.DeleteObjectAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string GetExtension(string contentType) =>
        contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "bin",
        };

    private static IEnumerable<string> GetObjectKeysToDelete(string key)
    {
        var extensionIndex = key.LastIndexOf('.');
        if (extensionIndex < 0 || !key.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            yield return key;
            yield break;
        }

        var nameWithoutExtension = key[..extensionIndex];
        var suffixIndex = nameWithoutExtension.LastIndexOf('-');
        if (suffixIndex < 0)
        {
            yield return key;
            yield break;
        }

        var suffix = nameWithoutExtension[(suffixIndex + 1)..];
        if (suffix is not ("64" or "128" or "256"))
        {
            yield return key;
            yield break;
        }

        var prefix = nameWithoutExtension[..suffixIndex];
        foreach (var size in ImageSharpAvatarImageProcessor.AvatarVariantSizes)
        {
            yield return $"{prefix}-{size}.webp";
        }
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
