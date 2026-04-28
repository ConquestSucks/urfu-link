using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
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
    MentionResolver mentionResolver,
    TimeProvider clock)
{
    public async Task<MessageDto> EditAsync(EditMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Edit must produce a non-empty body that fits the global length cap. Empty edits are
        // a UX request to "delete" — clients should call DeleteMessage instead so they don't
        // bypass author/TTL checks specific to deletion.
        if (string.IsNullOrEmpty(request.NewBody))
        {
            throw new ArgumentException(
                "New body cannot be empty. Use DeleteMessage to remove a message.", nameof(request));
        }
        if (request.NewBody.Length > ChatBodyConstraints.MaxBodyLength)
        {
            throw new ChatPayloadTooLargeException(
                $"Edited body exceeds {ChatBodyConstraints.MaxBodyLength} characters.");
        }

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

        var mentions = await mentionResolver
            .ResolveAsync(request.NewBody, conversation, opts.MaxMentionsPerMessage, cancellationToken)
            .ConfigureAwait(false);
        var priorMentions = new HashSet<Guid>(message.Mentions);
        var newlyAddedMentions = mentions.Where(m => !priorMentions.Contains(m)).ToList();
        var historyEntry = new EditHistoryEntry(message.Body, message.EditedAtUtc ?? message.CreatedAtUtc);

        var applied = await messages.ApplyEditAsync(
            message.Id, request.NewBody, mentions, historyEntry, now, cancellationToken).ConfigureAwait(false);
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

        if (newlyAddedMentions.Count > 0)
        {
            await dispatcher.PublishAsync(
                new ChatMentionCreatedEvent(
                    conversation.Id,
                    refreshed.Id,
                    request.CallerUserId,
                    newlyAddedMentions,
                    now),
                cancellationToken).ConfigureAwait(false);
        }

        var observers = conversation.Participants.ToList();
        var dto = MessageDto.FromDomain(refreshed);
        await broadcaster.NotifyMessageEditedAsync(observers, dto, cancellationToken).ConfigureAwait(false);

        return dto;
    }
}
