namespace UserService.Api.Domain.Interfaces;

public interface IAvatarStorage
{
    Task<string> UploadAsync(Guid userId, Stream fileStream, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectUrl, CancellationToken cancellationToken = default);
}
