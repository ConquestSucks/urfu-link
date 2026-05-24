namespace MediaService.Api.Domain.Interfaces;

/// <summary>
/// Generates short-lived signed URLs against the object store. The HTTP verb is
/// fixed by the method (PUT for uploads, GET for downloads). Browser-facing URLs
/// use the public endpoint while internal calls may resolve to the cluster DNS.
/// </summary>
public interface IPresignedUrlGenerator
{
    PresignedUrl ForUpload(string bucket, string objectKey, string contentType, TimeSpan ttl);

    PresignedUrl ForDownload(string bucket, string objectKey, string fileName, TimeSpan ttl);
}

public sealed record PresignedUrl(string Url, DateTimeOffset ExpiresAtUtc);
