namespace UserService.Api.Infrastructure.Storage;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string AvatarBucket { get; set; } = "user-avatars";
}
