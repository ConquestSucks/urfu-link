using System.Collections.Concurrent;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace ChatService.IntegrationTests.Infrastructure;

public sealed record GrantAccessCall(Guid AssetId, IReadOnlyList<Guid> UserIds, string ConversationId, Guid GrantedByUserId);

public sealed class FakeMediaServiceClient : IMediaServiceClient
{
    private readonly ConcurrentDictionary<Guid, MediaAssetMetadata> _assets = new();

    public ConcurrentBag<GrantAccessCall> Grants { get; } = [];

    /// <summary>
    /// Registers an asset with the given owner. Default metadata is an uploaded image. Override
    /// individual fields by passing them; <paramref name="isUploaded"/> set to false simulates an
    /// in-progress / failed upload that ChatService must reject.
    /// </summary>
    public void RegisterAsset(
        Guid assetId,
        Guid ownerId,
        AttachmentType kind = AttachmentType.Image,
        long sizeBytes = 1024,
        string mimeType = "image/png",
        string fileName = "asset.png",
        bool isUploaded = true,
        int? durationSeconds = null)
    {
        _assets[assetId] = new MediaAssetMetadata(assetId, ownerId, kind, sizeBytes, mimeType, fileName, isUploaded, durationSeconds);
    }

    public Task<IReadOnlyList<MediaAssetMetadata>> BatchGetMetadataAsync(
        IReadOnlyList<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<MediaAssetMetadata> result = assetIds
            .Where(_assets.ContainsKey)
            .Select(id => _assets[id])
            .ToList();
        return Task.FromResult(result);
    }

    public Task GrantConversationAccessAsync(
        Guid assetId,
        IReadOnlyList<Guid> userIds,
        string conversationId,
        Guid grantedByUserId,
        CancellationToken cancellationToken)
    {
        Grants.Add(new GrantAccessCall(assetId, userIds.ToList(), conversationId, grantedByUserId));
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _assets.Clear();
        Grants.Clear();
    }
}
