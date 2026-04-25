using Microsoft.Extensions.Options;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Mentions;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record EditMessageRequest(Guid MessageId, Guid CallerUserId, string NewBody);

public sealed class EditMessageService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IOptions<ChatOptions> options,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task<MessageDto> EditAsync(EditMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = await messages.GetByIdAsync(request.MessageId, cancellationToken).ConfigureAwait(false)
            ?? throw ChatMessageNotFoundException.For(request.MessageId);

        var conversation = await conversations.GetByIdAsync(message.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(message.ConversationId);

        if (!conversation.IsParticipant(request.CallerUserId))
        {
            throw new ChatAccessDeniedException(message.ConversationId, request.CallerUserId);
        }

        if (!message.IsAuthor(request.CallerUserId))
        {
            throw new ChatNotMessageAuthorException(message.Id, request.CallerUserId);
        }

        var opts = options.Value;
        var ttl = opts.EditTtl;
        var now = clock.GetUtcNow();
        if (now - message.CreatedAtUtc > ttl)
        {
            throw ChatEditTtlExpiredException.For(message.Id, ttl);
        }

        var newBody = request.NewBody ?? string.Empty;
        var mentions = MentionsParser.Parse(newBody, conversation.Participants, opts.MaxMentionsPerMessage);
        var historyEntry = new EditHistoryEntry(message.Body, message.EditedAtUtc ?? message.CreatedAtUtc);

        var applied = await messages.ApplyEditAsync(
            message.Id, newBody, mentions, historyEntry, now, cancellationToken).ConfigureAwait(false);
        if (!applied)
        {
            // Race: someone deleted the message between our read and write. Re-load and return current state.
            var stale = await messages.GetByIdAsync(message.Id, cancellationToken).ConfigureAwait(false);
            return stale is null ? MessageDto.FromDomain(message) : MessageDto.FromDomain(stale);
        }

        var refreshed = await messages.GetByIdAsync(message.Id, cancellationToken).ConfigureAwait(false)
            ?? message;

        await dispatcher.PublishAsync(
            new ChatMessageEditedEvent(
                conversation.Id,
                refreshed.Id,
                request.CallerUserId,
                refreshed.Body,
                refreshed.Mentions,
                refreshed.EditedAtUtc ?? now,
                now),
            cancellationToken).ConfigureAwait(false);

        var observers = conversation.Participants.ToList();
        var dto = MessageDto.FromDomain(refreshed);
        await broadcaster.NotifyMessageEditedAsync(observers, dto, cancellationToken).ConfigureAwait(false);

        return dto;
    }
}
