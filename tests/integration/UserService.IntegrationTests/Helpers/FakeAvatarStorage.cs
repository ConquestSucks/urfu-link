using System.Collections.Concurrent;
using UserService.Api.Domain.Interfaces;

namespace UserService.IntegrationTests.Helpers;

public sealed class FakeAvatarStorage : IAvatarStorage
{
    public ConcurrentDictionary<string, byte[]> Uploads { get; } = new();
    private readonly ConcurrentDictionary<string, string> _contentTypes = new();

    public Task<string> UploadAsync(Guid userId, ProcessedAvatar avatar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(avatar);

        var avatarId = Guid.NewGuid().ToString("N");
        string? canonicalUrl = null;
        foreach (var variant in avatar.Variants)
        {
            var extension = variant.ContentType switch
            {
                "image/jpeg" => "jpg",
                "image/png" => "png",
                "image/webp" => "webp",
                _ => "bin",
            };

            var key = $"avatars/{userId:N}/{avatarId}-{variant.Size}.{extension}";
            var url = $"http://localhost:9000/user-avatars/{key}";

            Uploads[url] = variant.Content.ToArray();
            _contentTypes[url] = variant.ContentType;

            if (variant.Size == avatar.CanonicalVariant.Size)
            {
                canonicalUrl = url;
            }
        }

        return Task.FromResult(canonicalUrl ?? throw new InvalidOperationException("No avatar variants were uploaded."));
    }

    public Task<AvatarObject?> DownloadAsync(string objectUrl, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Uploads.TryGetValue(objectUrl, out var content)
                ? new AvatarObject(content, _contentTypes.GetValueOrDefault(objectUrl, "application/octet-stream"))
                : null);
    }

    public Task DeleteAsync(string objectUrl, CancellationToken cancellationToken = default)
    {
        Uploads.TryRemove(objectUrl, out _);
        _contentTypes.TryRemove(objectUrl, out _);
        return Task.CompletedTask;
    }
}
