namespace UserService.Api.Domain.Interfaces;

public sealed record ProcessedAvatarVariant(int Size, ReadOnlyMemory<byte> Content, string ContentType);

public sealed record ProcessedAvatar(IReadOnlyList<ProcessedAvatarVariant> Variants)
{
    public ProcessedAvatarVariant CanonicalVariant =>
        Variants.OrderByDescending(variant => variant.Size).First();
}

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
