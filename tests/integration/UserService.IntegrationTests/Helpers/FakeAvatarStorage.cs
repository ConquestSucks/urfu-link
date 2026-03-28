using System.Collections.Concurrent;
using UserService.Api.Domain.Interfaces;

namespace UserService.IntegrationTests.Helpers;

public sealed class FakeAvatarStorage : IAvatarStorage
{
    public ConcurrentDictionary<string, byte[]> Uploads { get; } = new();

    public async Task<string> UploadAsync(Guid userId, Stream fileStream, string contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        var extension = contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "bin",
        };

        var key = $"avatars/{userId:N}/{Guid.NewGuid():N}.{extension}";
        var url = $"http://localhost:9000/user-avatars/{key}";

        using var ms = new MemoryStream();
        await fileStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        Uploads[url] = ms.ToArray();

        return url;
    }

    public Task DeleteAsync(string objectUrl, CancellationToken cancellationToken = default)
    {
        Uploads.TryRemove(objectUrl, out _);
        return Task.CompletedTask;
    }
}
