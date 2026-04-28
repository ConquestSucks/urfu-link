using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record DeleteMessageRequest(
    Guid MessageId,
    Guid CallerUserId,
    DeleteMode Mode,
    bool CallerIsAdmin = false);

public sealed class DeleteMessageService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IOptions<ChatOptions> options,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock,
    IDisciplineRoleResolver roleResolver)
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

        if (!conversation.IsParticipant(request.CallerUserId) && !request.CallerIsAdmin)
        {
            throw new ChatAccessDeniedException(message.ConversationId, request.CallerUserId);
        }

        if (request.Mode == DeleteMode.ForEveryone)
        {
            // Two paths to delete-for-everyone:
            //   1) author within TTL — the original direct-chat semantics
            //   2) moderator (Teacher in a discipline group, or admin everywhere) — TTL is
            //      bypassed because moderation is policy enforcement, not message authorship
            var canModerate = await roleResolver
                .CanModerateAsync(request.CallerUserId, request.CallerIsAdmin, conversation, cancellationToken)
                .ConfigureAwait(false);

            var now = clock.GetUtcNow();
            if (!canModerate)
            {
                if (!message.IsAuthor(request.CallerUserId))
                {
                    throw new ChatNotMessageAuthorException(message.Id, request.CallerUserId);
                }

                var opts = options.Value;
                var ttl = opts.DeleteForEveryoneTtl;
                if (now - message.CreatedAtUtc > ttl)
                {
                    throw ChatEditTtlExpiredException.For(message.Id, ttl);
                }
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

        // ForMe is purely local: hide for the caller in the message document and return the
        // current message state. No integration event is emitted (no consumer reacts to a
        // local hide) and no broadcast is sent (other participants must keep seeing the
        // message).
        await messages.AddHiddenForAsync(message.Id, request.CallerUserId, cancellationToken)
            .ConfigureAwait(false);

        var current = await messages.GetByIdAsync(message.Id, cancellationToken).ConfigureAwait(false);
        return current is null ? null : MessageDto.FromDomain(current);
    }
}
