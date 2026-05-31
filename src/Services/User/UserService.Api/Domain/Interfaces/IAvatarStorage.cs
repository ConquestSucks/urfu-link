namespace UserService.Api.Domain.Interfaces;

public sealed record AvatarObject(ReadOnlyMemory<byte> Content, string ContentType);

public interface IAvatarStorage
{
    Task<string> UploadAsync(Guid userId, ProcessedAvatar avatar, CancellationToken cancellationToken = default);
    Task<AvatarObject?> DownloadAsync(string objectUrl, CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectUrl, CancellationToken cancellationToken = default);
}
