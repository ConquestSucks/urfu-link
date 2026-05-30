using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Infrastructure.Storage;

public sealed class ImageSharpAvatarImageProcessor : IAvatarImageProcessor
{
    public const int AvatarSize = 256;
    public const int WebpQuality = 82;
    public const string WebpContentType = "image/webp";

    public async Task<ProcessedAvatar> ProcessAsync(
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        using var image = await Image.LoadAsync(fileStream, cancellationToken).ConfigureAwait(false);

        image.Mutate(context => context
            .AutoOrient()
            .Resize(new ResizeOptions
            {
                Size = new Size(AvatarSize, AvatarSize),
                Mode = ResizeMode.Crop,
                Sampler = KnownResamplers.Lanczos3,
            }));

        image.Metadata.ExifProfile = null;
        image.Metadata.IccProfile = null;

        await using var output = new MemoryStream();
        await image.SaveAsWebpAsync(
            output,
            new WebpEncoder { Quality = WebpQuality },
            cancellationToken).ConfigureAwait(false);

        return new ProcessedAvatar(output.ToArray(), WebpContentType);
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
}
