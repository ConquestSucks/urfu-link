using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using UserService.Api.Infrastructure.Storage;

namespace UserService.UnitTests.Infrastructure;

public sealed class AvatarImageProcessorTests
{
    [Fact]
    public async Task ProcessAsyncShouldCropResizeAndEncodeWebp()
    {
        using var sourceImage = new Image<Rgba32>(512, 320);
        await using var source = new MemoryStream();
        await sourceImage.SaveAsPngAsync(source, CancellationToken.None);
        source.Position = 0;

        var processor = new ImageSharpAvatarImageProcessor();

        var processed = await processor.ProcessAsync(
            source,
            "image/png",
            CancellationToken.None);

        Assert.Equal("image/webp", processed.ContentType);

        using var output = Image.Load(processed.Content.ToArray());
        Assert.Equal(256, output.Width);
        Assert.Equal(256, output.Height);
    }
}
