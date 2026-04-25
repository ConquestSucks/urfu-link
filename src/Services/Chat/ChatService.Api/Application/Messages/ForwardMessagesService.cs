using Microsoft.Extensions.Options;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

public sealed record ForwardMessagesRequest(
    string TargetConversationId,
    Guid CallerUserId,
    IReadOnlyList<Guid> MessageIds);

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

        var target = await conversations.GetByIdAsync(request.TargetConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(request.TargetConversationId);

        if (!target.IsParticipant(request.CallerUserId))
        {
            throw new ChatAccessDeniedException(request.TargetConversationId, request.CallerUserId);
        }

        // Load source messages individually so we can validate caller membership in each source
        // conversation. Forwarding is rare and capped at MaxForwardedMessages, so the N round-trips
        // are acceptable for the MVP.
        var sources = new List<Message>(request.MessageIds.Count);
        var sourceConversationCache = new Dictionary<string, Conversation>(StringComparer.Ordinal);
        foreach (var id in request.MessageIds)
        {
            var src = await messages.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
                ?? throw ChatMessageNotFoundException.For(id);

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

            sources.Add(src);
        }

        var now = clock.GetUtcNow();
        var produced = new List<MessageDto>(sources.Count);
        var lastPreview = (preview: (MessagePreview?)null, sentAt: now);
        var grantTasks = new List<Task>();
        var grantees = target.Participants.Where(p => p != request.CallerUserId).ToList();

        var index = 0;
        foreach (var src in sources)
        {
            var clientMessageId = $"forward:{src.Id:N}:{request.CallerUserId:N}:{now.UtcTicks}:{index++}";
            var sentAt = now.AddTicks(index);
            var newMessage = Message.Send(
                id: Guid.NewGuid(),
                conversationId: target.Id,
                senderId: request.CallerUserId,
                body: src.Body,
                attachments: src.Attachments,
                clientMessageId: clientMessageId,
                createdAtUtc: sentAt,
                forwardedFrom: new ForwardedFrom(src.SenderId, src.CreatedAtUtc, src.ConversationId));

            await messages.InsertAsync(newMessage, cancellationToken).ConfigureAwait(false);
            produced.Add(MessageDto.FromDomain(newMessage));

            lastPreview = (
                new MessagePreview(request.CallerUserId, newMessage.Body, sentAt, newMessage.HasAttachments),
                sentAt);

            // Grant access on each forwarded attachment to the new conversation participants.
            if (grantees.Count > 0)
            {
                foreach (var attachment in newMessage.Attachments)
                {
                    grantTasks.Add(mediaServiceClient.GrantConversationAccessAsync(
                        attachment.MediaAssetId, grantees, target.Id, request.CallerUserId, cancellationToken));
                }
            }

            await dispatcher.PublishAsync(
                new ChatMessageSentEvent(
                    target.Id,
                    newMessage.Id,
                    request.CallerUserId,
                    grantees,
                    BuildPreviewText(newMessage.Body, newMessage.HasAttachments),
                    newMessage.HasAttachments,
                    sentAt),
                cancellationToken).ConfigureAwait(false);

            await broadcaster.NotifyMessageReceivedAsync(
                grantees, MessageDto.FromDomain(newMessage), cancellationToken).ConfigureAwait(false);
        }

        if (grantTasks.Count > 0)
        {
            await Task.WhenAll(grantTasks).ConfigureAwait(false);
        }

        if (lastPreview.preview is { } preview)
        {
            await conversations.UpdateLastMessageAsync(
                target.Id, preview, lastPreview.sentAt, cancellationToken).ConfigureAwait(false);
        }

        return produced;
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
}
