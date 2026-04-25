using Microsoft.Extensions.Options;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record DeleteMessageRequest(Guid MessageId, Guid CallerUserId, DeleteMode Mode);

public sealed class DeleteMessageService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IOptions<ChatOptions> options,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task<MessageDto?> DeleteAsync(DeleteMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = await messages.GetByIdAsync(request.MessageId, cancellationToken).ConfigureAwait(false);
        if (message is null)
        {
            return null;
        }

        var conversation = await conversations.GetByIdAsync(message.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(message.ConversationId);

        if (!conversation.IsParticipant(request.CallerUserId))
        {
            throw new ChatAccessDeniedException(message.ConversationId, request.CallerUserId);
        }

        if (request.Mode == DeleteMode.ForEveryone)
        {
            if (!message.IsAuthor(request.CallerUserId))
            {
                throw new ChatNotMessageAuthorException(message.Id, request.CallerUserId);
            }

            var opts = options.Value;
            var ttl = opts.DeleteForEveryoneTtl;
            var now = clock.GetUtcNow();
            if (now - message.CreatedAtUtc > ttl)
            {
                throw ChatEditTtlExpiredException.For(message.Id, ttl);
            }

            var applied = await messages.ApplyDeleteForEveryoneAsync(
                message.Id, request.CallerUserId, now, cancellationToken).ConfigureAwait(false);
            if (!applied)
            {
                var existing = await messages.GetByIdAsync(message.Id, cancellationToken).ConfigureAwait(false);
                return existing is null ? null : MessageDto.FromDomain(existing);
            }

            await dispatcher.PublishAsync(
                new ChatMessageDeletedEvent(
                    conversation.Id,
                    message.Id,
                    DeleteMode.ForEveryone,
                    request.CallerUserId,
                    now),
                cancellationToken).ConfigureAwait(false);

            await broadcaster.NotifyMessageDeletedAsync(
                conversation.Participants.ToList(),
                conversation.Id,
                message.Id,
                DeleteMode.ForEveryone,
                request.CallerUserId,
                cancellationToken).ConfigureAwait(false);

            var refreshed = await messages.GetByIdAsync(message.Id, cancellationToken).ConfigureAwait(false);
            return refreshed is null ? null : MessageDto.FromDomain(refreshed);
        }

        // ForMe: hide locally, no broadcast.
        var added = await messages.AddHiddenForAsync(message.Id, request.CallerUserId, cancellationToken)
            .ConfigureAwait(false);
        if (added)
        {
            var nowForMe = clock.GetUtcNow();
            await dispatcher.PublishAsync(
                new ChatMessageDeletedEvent(
                    conversation.Id,
                    message.Id,
                    DeleteMode.ForMe,
                    request.CallerUserId,
                    nowForMe),
                cancellationToken).ConfigureAwait(false);
        }

        var current = await messages.GetByIdAsync(message.Id, cancellationToken).ConfigureAwait(false);
        return current is null ? null : MessageDto.FromDomain(current);
    }
}
