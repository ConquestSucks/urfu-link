using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Conversations;

public sealed record PinMessageRequest(
    string ConversationId,
    Guid CallerUserId,
    bool CallerIsAdmin,
    Guid MessageId);

public sealed class PinMessageService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IDisciplineRoleResolver roleResolver,
    IOptions<ChatOptions> options,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<MessageDto>> PinAsync(PinMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = await conversations.GetByIdAsync(request.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(request.ConversationId);

        // Admins can manage pins on any conversation (Q4 in #207 review). The
        // resolver short-circuits the membership and role checks for them; for
        // every other caller we still require participation + the per-type rule.
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

        var message = await messages.GetByIdAsync(request.MessageId, cancellationToken).ConfigureAwait(false)
            ?? throw ChatMessageNotFoundException.For(request.MessageId);

        if (!string.Equals(message.ConversationId, conversation.Id, StringComparison.Ordinal))
        {
            throw ChatMessageNotFoundException.For(request.MessageId);
        }

        if (message.State == MessageState.Deleted)
        {
            throw ChatMessageNotFoundException.For(request.MessageId);
        }

        var opts = options.Value;
        if (conversation.IsPinned(request.MessageId))
        {
            // Idempotent: return current pinned list without re-publishing or re-broadcasting.
            return await BuildPinnedDtosAsync(conversation, cancellationToken).ConfigureAwait(false);
        }

        var pinned = await conversations.AddPinnedMessageAsync(
            conversation.Id, request.MessageId, opts.MaxPinnedMessages, cancellationToken).ConfigureAwait(false);
        if (!pinned)
        {
            throw new ChatPinLimitExceededException(conversation.Id, opts.MaxPinnedMessages);
        }

        var now = clock.GetUtcNow();
        await dispatcher.PublishAsync(
            new ChatMessagePinnedEvent(
                conversation.Id,
                request.MessageId,
                request.CallerUserId,
                now,
                conversation.Participants),
            cancellationToken).ConfigureAwait(false);

        var dtos = await BuildPinnedDtosAsync(
            await conversations.GetByIdAsync(conversation.Id, cancellationToken).ConfigureAwait(false) ?? conversation,
            cancellationToken).ConfigureAwait(false);
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
