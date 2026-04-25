namespace Urfu.Link.Services.Chat.Application.Messages;

/// <summary>
/// Abstraction over MediaService gRPC. Decouples the chat application layer from generated
/// gRPC types so it can be easily faked in tests and swapped if the contract evolves.
/// </summary>
public interface IMediaServiceClient
{
    Task<bool> CheckOwnershipAsync(Guid assetId, Guid userId, CancellationToken cancellationToken);

    Task GrantConversationAccessAsync(
        Guid assetId,
        IReadOnlyList<Guid> userIds,
        string conversationId,
        Guid grantedByUserId,
        CancellationToken cancellationToken);
}
