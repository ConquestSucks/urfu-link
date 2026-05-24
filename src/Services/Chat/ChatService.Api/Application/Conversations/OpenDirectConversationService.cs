using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace Urfu.Link.Services.Chat.Application.Conversations;

/// <summary>
/// Opens a direct conversation between the caller and a peer. When no persisted conversation
/// exists yet, returns a deterministic draft only. The document is materialized by the first
/// successfully stored message, so empty direct chats never appear in either participant's list.
/// </summary>
public sealed class OpenDirectConversationService(
    IConversationRepository repository,
    TimeProvider clock)
{
    public async Task<Conversation> OpenAsync(Guid callerUserId, Guid peerUserId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var draft = Conversation.OpenDirect(callerUserId, peerUserId, now);

        var existing = await repository.GetByIdAsync(draft.Id, cancellationToken).ConfigureAwait(false);
        return existing ?? draft;
    }
}
