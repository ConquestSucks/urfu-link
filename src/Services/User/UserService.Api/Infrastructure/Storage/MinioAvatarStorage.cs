using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Infrastructure.Storage;

public sealed class MinioAvatarStorage(
    IAmazonS3 s3Client,
    IOptions<StorageOptions> options) : IAvatarStorage
{
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

        await s3Client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);

        return $"{_options.Endpoint.TrimEnd('/')}/{_options.AvatarBucket}/{key}";
    }

    public async Task DeleteAsync(string objectUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(objectUrl);

        var prefix = $"{_options.Endpoint.TrimEnd('/')}/{_options.AvatarBucket}/";
        if (!objectUrl.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var key = objectUrl[prefix.Length..];

        var deleteRequest = new DeleteObjectRequest
        {
            BucketName = _options.AvatarBucket,
            Key = key,
        };

        await s3Client.DeleteObjectAsync(deleteRequest, cancellationToken).ConfigureAwait(false);
    }
}
