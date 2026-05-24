using Amazon.S3;
using Amazon.S3.Model;
using MediaService.Api.Domain.Interfaces;

namespace MediaService.Api.Infrastructure.Storage;

public sealed class MinioObjectStorage(IAmazonS3 s3Client) : IMediaObjectStorage
{
    public async Task DeleteAsync(string bucket, string objectKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        var request = new DeleteObjectRequest { BucketName = bucket, Key = objectKey };
        await s3Client.DeleteObjectAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ObjectMetadata?> GetMetadataAsync(
        string bucket, string objectKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bucket);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);

        var request = new GetObjectMetadataRequest { BucketName = bucket, Key = objectKey };
        try
        {
            var response = await s3Client.GetObjectMetadataAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return new ObjectMetadata(
                ContentLength: response.ContentLength,
                ETag: response.ETag,
                ContentType: response.Headers["Content-Type"]);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
