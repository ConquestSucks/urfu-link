using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record ForwardMessagesRequest(
    string TargetConversationId,
    Guid CallerUserId,
    IReadOnlyList<Guid> MessageIds,
    Guid? PeerUserId = null);

public sealed class ForwardMessagesService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IMediaServiceClient mediaServiceClient,
    IOptions<ChatOptions> options,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<MessageDto>> ForwardAsync(
        ForwardMessagesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.MessageIds);

        var opts = options.Value;
        if (request.MessageIds.Count == 0)
        {
            return Array.Empty<MessageDto>();
        }

        if (request.MessageIds.Count > opts.MaxForwardedMessages)
        {
            throw new ChatForwardLimitExceededException(request.MessageIds.Count, opts.MaxForwardedMessages);
        }

        var (target, shouldCreateTarget) = await ResolveTargetConversationAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!target.IsParticipant(request.CallerUserId))
        {
            throw new ChatAccessDeniedException(request.TargetConversationId, request.CallerUserId);
        }

        // Single bulk read of all source messages — the previous N+1 had a round-trip per id.
        var loadedById = (await messages.GetManyAsync(request.MessageIds, cancellationToken).ConfigureAwait(false))
            .ToDictionary(m => m.Id);

        // Preserve caller-supplied ordering (forward [c, a, b] should land as [c, a, b] in target).
        var sources = new List<Message>(request.MessageIds.Count);
        foreach (var id in request.MessageIds)
        {
            if (!loadedById.TryGetValue(id, out var src))
            {
                throw ChatMessageNotFoundException.For(id);
            }
            sources.Add(src);
        }

        // One conversation lookup per unique source — caller membership must hold for every
        // source the user is forwarding from.
        var sourceConversationCache = new Dictionary<string, Conversation>(StringComparer.Ordinal);
        foreach (var src in sources)
        {
            if (!sourceConversationCache.TryGetValue(src.ConversationId, out var srcConv))
            {
                srcConv = await conversations.GetByIdAsync(src.ConversationId, cancellationToken).ConfigureAwait(false)
                    ?? throw ConversationNotFoundException.For(src.ConversationId);
                sourceConversationCache[src.ConversationId] = srcConv;
            }
            if (!srcConv.IsParticipant(request.CallerUserId))
            {
                throw new ChatAccessDeniedException(src.ConversationId, request.CallerUserId);
            }
        }

        var now = clock.GetUtcNow();
        var producedMessages = new List<Message>(sources.Count);
        var produced = new List<MessageDto>(sources.Count);
        var lastPreview = (preview: (MessagePreview?)null, sentAt: now);
        var forwardedAttachmentIds = new List<Guid>();
        var grantees = target.Participants.Where(p => p != request.CallerUserId).ToList();

        var index = 0;
        foreach (var src in sources)
        {
            var sentAt = now.AddMilliseconds(index);
            var clientMessageId = $"forward:{src.Id:N}:{request.CallerUserId:N}:{now.UtcTicks}:{index}";

            // Hide source conversation id when forwarding into a group conversation — the
            // forwarder shouldn't leak which (potentially private) chat the message came from.
            // For direct forwards we keep it so the recipient can attribute the origin.
            var originalConversationId = target.Type == ConversationType.Group
                ? null
                : src.ConversationId;

            var newMessage = Message.Send(
                id: Guid.NewGuid(),
                conversationId: target.Id,
                senderId: request.CallerUserId,
                body: src.Body,
                attachments: src.Attachments,
                clientMessageId: clientMessageId,
                createdAtUtc: sentAt,
                forwardedFrom: new ForwardedFrom(src.SenderId, src.CreatedAtUtc, originalConversationId),
                authorRole: target.RoleOf(request.CallerUserId));

            await messages.InsertAsync(newMessage, cancellationToken).ConfigureAwait(false);
            producedMessages.Add(newMessage);
            produced.Add(MessageDto.FromDomain(newMessage));

            lastPreview = (
                BuildMessagePreview(newMessage),
                sentAt);

            // Grant access on each forwarded attachment to the new conversation participants.
            if (grantees.Count > 0)
            {
                foreach (var attachment in newMessage.Attachments)
                {
                    forwardedAttachmentIds.Add(attachment.MediaAssetId);
                }
            }

            index++;
        }

        if (shouldCreateTarget)
        {
            target = await EnsureConversationCreatedAsync(target, now, cancellationToken)
                .ConfigureAwait(false);
        }

        if (forwardedAttachmentIds.Count > 0)
        {
            var grantTasks = forwardedAttachmentIds.Select(assetId =>
                mediaServiceClient.GrantConversationAccessAsync(
                    assetId, grantees, target.Id, request.CallerUserId, cancellationToken));
            await Task.WhenAll(grantTasks).ConfigureAwait(false);
        }

        if (lastPreview.preview is { } preview)
        {
            await conversations.UpdateLastMessageAsync(
                target.Id, preview, lastPreview.sentAt, cancellationToken).ConfigureAwait(false);
            target.RegisterMessage(preview, lastPreview.sentAt);
        }

        for (var i = 0; i < producedMessages.Count; i++)
        {
            var message = producedMessages[i];
            await dispatcher.PublishAsync(
                new ChatMessageSentEvent(
                    target.Id,
                    message.Id,
                    request.CallerUserId,
                    grantees,
                    BuildPreviewText(message.Body, message.HasAttachments),
                    message.HasAttachments,
                    message.CreatedAtUtc),
                cancellationToken).ConfigureAwait(false);
        }

        if (producedMessages.Count > 0)
        {
            await broadcaster.NotifyConversationUpdatedAsync(
                target.Participants,
                ConversationDto.FromDomain(target).WithLastMessageMetadata(producedMessages[^1]),
                cancellationToken).ConfigureAwait(false);

            foreach (var dto in produced)
            {
                await broadcaster.NotifyMessageReceivedAsync(
                    grantees, dto, cancellationToken).ConfigureAwait(false);
            }
        }

        return produced;
    }

    private async Task<(Conversation Conversation, bool ShouldCreateConversation)> ResolveTargetConversationAsync(
        ForwardMessagesRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await conversations.GetByIdAsync(request.TargetConversationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return (existing, false);
        }

        if (request.PeerUserId is not { } peerUserId)
        {
            throw ConversationNotFoundException.For(request.TargetConversationId);
        }

        var draft = Conversation.OpenDirect(request.CallerUserId, peerUserId, clock.GetUtcNow());
        if (!string.Equals(draft.Id, request.TargetConversationId, StringComparison.Ordinal))
        {
            throw ConversationNotFoundException.For(request.TargetConversationId);
        }

        return (draft, true);
    }

    private async Task<Conversation> EnsureConversationCreatedAsync(
        Conversation conversation,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        var created = await conversations.TryCreateAsync(conversation, cancellationToken).ConfigureAwait(false);
        if (!created)
        {
            return await conversations.GetByIdAsync(conversation.Id, cancellationToken).ConfigureAwait(false)
                ?? throw ConversationNotFoundException.For(conversation.Id);
        }

        await dispatcher.PublishAsync(
            new ChatConversationCreatedEvent(
                conversation.Id,
                conversation.Type,
                conversation.Participants,
                occurredAtUtc),
            cancellationToken).ConfigureAwait(false);

        return conversation;
    }

    private static string BuildPreviewText(string body, bool hasAttachments)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return hasAttachments ? "[attachment]" : string.Empty;
        }

        const int maxPreview = 120;
        if (body.Length <= maxPreview)
        {
            return body;
        }

        var cutoff = char.IsHighSurrogate(body[maxPreview - 1]) ? maxPreview - 1 : maxPreview;
        return body[..cutoff];
    }

    private static MessagePreview BuildMessagePreview(Message message) => new(
        message.SenderId,
        message.Body,
        message.CreatedAtUtc,
        message.HasAttachments,
        message.Attachments.Select(a => a.FileName).ToList(),
        message.Attachments.Select(a => a.Type).ToList());
}
