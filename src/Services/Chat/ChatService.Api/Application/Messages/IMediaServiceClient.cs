using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Application.Messages;

/// <summary>
/// Server-side projection of a single asset returned by MediaService. Used by ChatService to
/// build an <see cref="Domain.ValueObjects.Attachment"/> from a client-supplied asset id without
/// trusting any client metadata.
/// </summary>
public sealed record MediaAssetMetadata(
    Guid AssetId,
    Guid OwnerId,
    AttachmentType Kind,
    long SizeBytes,
    string MimeType,
    string OriginalFileName,
    bool IsUploaded,
    int? DurationSeconds = null);

/// <summary>
/// Abstraction over MediaService gRPC. Decouples the chat application layer from generated
/// gRPC types so it can be easily faked in tests and swapped if the contract evolves.
/// </summary>
public interface IMediaServiceClient
{
    /// <summary>
    /// Fetches authoritative metadata for the given asset ids. The returned list contains an
    /// entry for every asset that exists in MediaService — missing assets are silently dropped.
    /// </summary>
    Task<IReadOnlyList<MediaAssetMetadata>> BatchGetMetadataAsync(
        IReadOnlyList<Guid> assetIds,
        CancellationToken cancellationToken);

    Task GrantConversationAccessAsync(
        Guid assetId,
        IReadOnlyList<Guid> userIds,
        string conversationId,
        Guid grantedByUserId,
        CancellationToken cancellationToken);
}
