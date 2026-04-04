namespace UserService.Api.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; set; } = "http://localhost:9000";
    /// <summary>
    /// Public-facing base URL used when generating avatar links returned to clients.
    /// Defaults to <see cref="Endpoint"/> when not set.
    /// Override in dev with the browser-accessible host (e.g. http://localhost:9000).
    /// </summary>
    public string? PublicEndpoint { get; set; }
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string AvatarBucket { get; set; } = "user-avatars";
}
