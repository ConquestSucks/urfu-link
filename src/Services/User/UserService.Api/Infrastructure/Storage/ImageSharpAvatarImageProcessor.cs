using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Infrastructure.Storage;

public sealed class ImageSharpAvatarImageProcessor : IAvatarImageProcessor
{
    public const int AvatarSize = 256;
    public static readonly int[] AvatarVariantSizes = [64, 128, AvatarSize];
    public const int SmallWebpQuality = 68;
    public const int MediumWebpQuality = 72;
    public const int LargeWebpQuality = 76;
    public const string WebpContentType = "image/webp";

    public async Task<ProcessedAvatar> ProcessAsync(
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        using var image = await Image.LoadAsync(fileStream, cancellationToken).ConfigureAwait(false);

        image.Mutate(context => context.AutoOrient());

        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;

        var variants = new List<ProcessedAvatarVariant>(AvatarVariantSizes.Length);
        foreach (var size in AvatarVariantSizes)
        {
            using var resized = image.Clone(context => context.Resize(new ResizeOptions
            {
                Size = new Size(size, size),
                Mode = ResizeMode.Crop,
                Sampler = KnownResamplers.Lanczos3,
            }));

            await using var output = new MemoryStream();
            await resized.SaveAsWebpAsync(
                output,
                CreateEncoder(size),
                cancellationToken).ConfigureAwait(false);

            variants.Add(new ProcessedAvatarVariant(size, output.ToArray(), WebpContentType));
        }

        return new ProcessedAvatar(variants);
    }

    public async Task<bool> IsNormalizedAsync(
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (!string.Equals(contentType, WebpContentType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var info = await Image.IdentifyAsync(fileStream, cancellationToken).ConfigureAwait(false);
        return info is not null && info.Width <= AvatarSize && info.Height <= AvatarSize;
    }

    private static WebpEncoder CreateEncoder(int size) => new()
    {
        FileFormat = WebpFileFormatType.Lossy,
        Method = WebpEncodingMethod.BestQuality,
        Quality = size switch
        {
            <= 64 => SmallWebpQuality,
            <= 128 => MediumWebpQuality,
            _ => LargeWebpQuality,
        },
    };
}
