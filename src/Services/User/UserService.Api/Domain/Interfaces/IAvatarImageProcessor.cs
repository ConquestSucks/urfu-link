namespace UserService.Api.Domain.Interfaces;

public sealed record ProcessedAvatar(ReadOnlyMemory<byte> Content, string ContentType);

public interface IAvatarImageProcessor
{
    Task<ProcessedAvatar> ProcessAsync(
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<bool> IsNormalizedAsync(
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default);
}
