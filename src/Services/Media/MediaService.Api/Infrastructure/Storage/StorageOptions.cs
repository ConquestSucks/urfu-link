using System.ComponentModel.DataAnnotations;

namespace MediaService.Api.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required(AllowEmptyStrings = false)]
    public string Endpoint { get; set; } = "http://localhost:9000";

    /// <summary>
    /// Public-facing base URL used when generating presigned URLs returned to
    /// browser clients. Defaults to <see cref="Endpoint"/> when not set.
    /// </summary>
    public string? PublicEndpoint { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string AccessKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string SecretKey { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string PrivateBucket { get; set; } = "media-private";

    [Required(AllowEmptyStrings = false)]
    public string PublicBucket { get; set; } = "media-public";

    public TimeSpan UploadUrlTtl { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan DownloadUrlTtl { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan UploadSessionTtl { get; set; } = TimeSpan.FromMinutes(30);
}
