using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Conversations;

public sealed record UnpinMessageRequest(
    string ConversationId,
    Guid CallerUserId,
    bool CallerIsAdmin,
    Guid MessageId);

public sealed class UnpinMessageService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IDisciplineRoleResolver roleResolver,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<MessageDto>> UnpinAsync(UnpinMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = await conversations.GetByIdAsync(request.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(request.ConversationId);

        if (!request.CallerIsAdmin && !conversation.IsParticipant(request.CallerUserId))
        {
            throw new ChatAccessDeniedException(request.ConversationId, request.CallerUserId);
        }

        var canPin = await roleResolver
            .CanPinAsync(request.CallerUserId, request.CallerIsAdmin, conversation, cancellationToken)
            .ConfigureAwait(false);
        if (!canPin)
        {
            throw new ChatAccessDeniedException(request.ConversationId, request.CallerUserId);
        }

        var removed = await conversations.RemovePinnedMessageAsync(
            conversation.Id, request.MessageId, cancellationToken).ConfigureAwait(false);
        if (!removed)
        {
            // Already unpinned — return current pinned list, no event, no broadcast.
            return await BuildPinnedDtosAsync(conversation, cancellationToken).ConfigureAwait(false);
        }

        var now = clock.GetUtcNow();
        await dispatcher.PublishAsync(
            new ChatMessageUnpinnedEvent(
                conversation.Id,
                request.MessageId,
                request.CallerUserId,
                now,
                conversation.Participants),
            cancellationToken).ConfigureAwait(false);

        var refreshed = await conversations.GetByIdAsync(conversation.Id, cancellationToken).ConfigureAwait(false) ?? conversation;
        var dtos = await BuildPinnedDtosAsync(refreshed, cancellationToken).ConfigureAwait(false);
        await broadcaster.NotifyPinsUpdatedAsync(
            conversation.Participants.ToList(), conversation.Id, dtos, cancellationToken).ConfigureAwait(false);

        return dtos;
    }

    private async Task<IReadOnlyList<MessageDto>> BuildPinnedDtosAsync(
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        if (conversation.PinnedMessageIds.Count == 0)
        {
            return Array.Empty<MessageDto>();
        }

        var pinnedMessages = await messages.GetByIdsAsync(
            conversation.Id, conversation.PinnedMessageIds, cancellationToken).ConfigureAwait(false);
        return GetPinnedMessagesQuery.OrderPinnedMessages(conversation, pinnedMessages);
    }
}
