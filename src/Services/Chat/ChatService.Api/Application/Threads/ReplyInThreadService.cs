using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Mentions;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Threads;

public sealed record ReplyInThreadRequest(
    Guid SenderId,
    Guid RootMessageId,
    string Body,
    IReadOnlyList<Guid> AttachmentAssetIds,
    Guid? ReplyToMessageId,
    string ClientMessageId);

/// <summary>
/// Posts a reply in the thread rooted at <see cref="ReplyInThreadRequest.RootMessageId"/>.
/// The reply lives in the same <c>messages</c> collection as the main flow but carries a
/// non-null <c>ThreadRootId</c>, so it is excluded from <c>ListByConversationAsync</c> and
/// surfaces only via thread-specific queries.
///
/// Subscriptions are upserted in <c>(Replied | Mentioned)</c> reasons before the broadcast so
/// the freshly-subscribed users immediately receive <c>ThreadReplyReceived</c>. The denorm
/// update on the root is non-transactional w.r.t. the reply insert: the reply is written first
/// and the counter follows, leaving at most a millisecond window in which the count lags. This
/// is acceptable for a UI marker — clients re-render on <c>ThreadRootUpdated</c>.
/// </summary>
public sealed class ReplyInThreadService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IThreadSubscriptionRepository subscriptions,
    IMediaServiceClient mediaServiceClient,
    IIdempotencyStore idempotencyStore,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    TimeProvider clock,
    IOptions<ChatOptions> options)
{
    public async Task<MessageDto> ReplyAsync(ReplyInThreadRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidatePayloadShape(request);

        var root = await messages.GetByIdAsync(request.RootMessageId, cancellationToken).ConfigureAwait(false)
            ?? throw ChatThreadRootNotFoundException.For(request.RootMessageId);

        if (root.IsThreadReply)
        {
            throw ChatThreadCannotReplyToReplyException.For(root.Id);
        }

        // Tombstoned roots are surfaced as "not found" to callers — the application contract is
        // that you cannot post into a thread whose origin has been deleted for everyone.
        if (root.State == MessageState.Deleted)
        {
            throw ChatThreadRootNotFoundException.For(request.RootMessageId);
        }

        var conversation = await conversations.GetByIdAsync(root.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(root.ConversationId);

        if (!conversation.IsParticipant(request.SenderId))
        {
            throw new ChatAccessDeniedException(conversation.Id, request.SenderId);
        }

        // Idempotency on (senderId, clientMessageId) — same key shape as SendMessage but in a
        // thread-scoped namespace so a main-flow client message id and a thread-reply client
        // message id from the same sender don't collide.
        var idempotencyKey = $"chat:thread:{request.SenderId:N}:{request.ClientMessageId}";
        var firstAttempt = await idempotencyStore.TryRegisterAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (!firstAttempt)
        {
            var prior = await messages.FindByClientMessageIdAsync(request.SenderId, request.ClientMessageId, cancellationToken).ConfigureAwait(false);
            if (prior is not null)
            {
                return MessageDto.FromDomain(prior);
            }
        }

        var attachments = await ResolveAttachmentsAsync(request.AttachmentAssetIds, request.SenderId, cancellationToken).ConfigureAwait(false);
        var replyTo = await ResolveReplyToInThreadAsync(root, request.ReplyToMessageId, cancellationToken).ConfigureAwait(false);

        var opts = options.Value;
        var mentions = MentionsParser.Parse(request.Body, conversation.Participants, opts.MaxMentionsPerMessage);

        var now = clock.GetUtcNow();
        var reply = Message.SendAsThreadReply(
            id: Guid.NewGuid(),
            conversationId: conversation.Id,
            senderId: request.SenderId,
            body: request.Body,
            attachments: attachments,
            clientMessageId: request.ClientMessageId,
            createdAtUtc: now,
            threadRootId: root.Id,
            mentions: mentions,
            replyTo: replyTo,
            authorRole: conversation.RoleOf(request.SenderId));

        try
        {
            await messages.InsertAsync(reply, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateClientMessageException)
        {
            var prior = await messages.FindByClientMessageIdAsync(request.SenderId, request.ClientMessageId, cancellationToken).ConfigureAwait(false);
            if (prior is not null)
            {
                return MessageDto.FromDomain(prior);
            }
            throw;
        }

        // Bump root denorms after the reply is durably written. Mongo single-replica has no
        // multi-document transactions; on rare crash between the two writes the reply exists
        // but the count lags. Acceptable — the count is a UI hint, and ThreadRootUpdated will
        // re-broadcast it on the next reply.
        await messages.IncrementThreadDenormAsync(root.Id, request.SenderId, now, cancellationToken).ConfigureAwait(false);

        await GrantAttachmentAccessAsync(attachments, conversation, request.SenderId, cancellationToken).ConfigureAwait(false);

        // Auto-subscribe: the replier is always Replied; mentioned users are Mentioned (escalated
        // if they were already Manual). Self-mention does not change the replier's reason.
        var newSubscriberEvents = new List<(Guid UserId, ThreadSubscriptionReason Reason)>();

        var replierUpsert = await subscriptions.UpsertAsync(
            ThreadSubscription.Subscribe(root.Id, request.SenderId, ThreadSubscriptionReason.Replied, now, lastActivityAtUtc: now),
            cancellationToken).ConfigureAwait(false);
        if (replierUpsert.RequiresEvent)
        {
            newSubscriberEvents.Add((request.SenderId, ThreadSubscriptionReason.Replied));
        }

        foreach (var mentionedUserId in mentions)
        {
            if (mentionedUserId == request.SenderId)
            {
                continue;
            }
            var upsert = await subscriptions.UpsertAsync(
                ThreadSubscription.Subscribe(root.Id, mentionedUserId, ThreadSubscriptionReason.Mentioned, now, lastActivityAtUtc: now),
                cancellationToken).ConfigureAwait(false);
            if (upsert.RequiresEvent)
            {
                newSubscriberEvents.Add((mentionedUserId, ThreadSubscriptionReason.Mentioned));
            }
        }

        // Bump lastActivityAtUtc on every existing subscription (including the ones we just
        // upserted — the upsert already set it, but this also covers any subscriber whose
        // reason did not change yet whose active-threads ordering must advance).
        await subscriptions.TouchActivityForRootAsync(root.Id, now, cancellationToken).ConfigureAwait(false);

        var subscribers = await subscriptions.GetSubscriberIdsAsync(root.Id, cancellationToken).ConfigureAwait(false);

        await dispatcher.PublishAsync(
            new ChatThreadReplyPostedEvent(
                conversation.Id,
                root.Id,
                reply.Id,
                request.SenderId,
                subscribers,
                Mentions: mentions.Count == 0 ? null : mentions,
                now),
            cancellationToken).ConfigureAwait(false);

        if (mentions.Count > 0)
        {
            await dispatcher.PublishAsync(
                new ChatMentionCreatedEvent(
                    conversation.Id,
                    reply.Id,
                    request.SenderId,
                    mentions,
                    now,
                    ThreadRootId: root.Id),
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var (userId, reason) in newSubscriberEvents)
        {
            await dispatcher.PublishAsync(
                new ChatThreadSubscriptionChangedEvent(root.Id, userId, Subscribed: true, reason, now),
                cancellationToken).ConfigureAwait(false);
        }

        var replyDto = MessageDto.FromDomain(reply);
        await broadcaster.NotifyThreadReplyReceivedAsync(subscribers, root.Id, replyDto, cancellationToken).ConfigureAwait(false);

        // Compute the new root denorms in-memory for the broadcast — re-loading the root just to
        // read what we already know is wasteful, and the IncrementThreadDenormAsync write above
        // is the source of truth.
        var newReplyCount = root.ThreadReplyCount + 1;
        IReadOnlyList<Guid> newParticipants = root.ThreadParticipants.Contains(request.SenderId)
            ? root.ThreadParticipants
            : root.ThreadParticipants.Append(request.SenderId).ToList();

        await broadcaster.NotifyThreadRootUpdatedAsync(
            conversation.Participants,
            conversation.Id,
            root.Id,
            newReplyCount,
            newParticipants,
            now,
            cancellationToken).ConfigureAwait(false);

        return replyDto;
    }

    private static void ValidatePayloadShape(ReplyInThreadRequest request)
    {
        if (request.Body is { Length: > ChatBodyConstraints.MaxBodyLength })
        {
            throw new ChatPayloadTooLargeException(
                $"Reply body exceeds {ChatBodyConstraints.MaxBodyLength} characters.");
        }

        if (request.AttachmentAssetIds is { Count: > ChatBodyConstraints.MaxAttachmentsPerMessage })
        {
            throw new ChatPayloadTooLargeException(
                $"Reply has more than {ChatBodyConstraints.MaxAttachmentsPerMessage} attachments.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientMessageId))
        {
            throw new ArgumentException("ClientMessageId is required.", nameof(request));
        }
    }

    private async Task<ReplyTo?> ResolveReplyToInThreadAsync(
        Message root,
        Guid? replyToMessageId,
        CancellationToken cancellationToken)
    {
        if (replyToMessageId is not { } id)
        {
            return null;
        }

        var target = await messages.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            throw ChatThreadReplyTargetNotInThreadException.For(id, root.Id);
        }

        // The quote target must be either the thread root itself or another reply within the
        // same thread. Quoting across threads or out of the main flow is rejected because the
        // peer rendering would otherwise pull in a body the user is not viewing.
        var isInThread = target.Id == root.Id || target.ThreadRootId == root.Id;
        if (!isInThread)
        {
            throw ChatThreadReplyTargetNotInThreadException.For(id, root.Id);
        }

        return ReplyTo.Create(target.Id, target.SenderId, target.Body);
    }

    private async Task<IReadOnlyList<Attachment>> ResolveAttachmentsAsync(
        IReadOnlyList<Guid> assetIds,
        Guid senderId,
        CancellationToken cancellationToken)
    {
        if (assetIds is null || assetIds.Count == 0)
        {
            return Array.Empty<Attachment>();
        }

        var metadata = await mediaServiceClient.BatchGetMetadataAsync(assetIds, cancellationToken).ConfigureAwait(false);
        var byId = metadata.ToDictionary(m => m.AssetId);

        var resolved = new List<Attachment>(assetIds.Count);
        foreach (var assetId in assetIds)
        {
            if (!byId.TryGetValue(assetId, out var meta) || !meta.IsUploaded)
            {
                throw new ChatAttachmentNotOwnedException(assetId, senderId);
            }
            if (meta.OwnerId != senderId)
            {
                throw new ChatAttachmentNotOwnedException(assetId, senderId);
            }
            resolved.Add(new Attachment(
                MediaAssetId: meta.AssetId,
                Type: meta.Kind,
                ThumbnailAssetId: null,
                FileName: meta.OriginalFileName,
                Size: meta.SizeBytes,
                MimeType: meta.MimeType));
        }
        return resolved;
    }

    private async Task GrantAttachmentAccessAsync(
        IReadOnlyList<Attachment> attachments,
        Conversation conversation,
        Guid senderId,
        CancellationToken cancellationToken)
    {
        if (attachments.Count == 0)
        {
            return;
        }

        var grantees = conversation.Participants.Where(p => p != senderId).ToList();
        if (grantees.Count == 0)
        {
            return;
        }

        var grantTasks = attachments
            .Select(a => mediaServiceClient.GrantConversationAccessAsync(
                a.MediaAssetId, grantees, conversation.Id, senderId, cancellationToken))
            .ToArray();
        await Task.WhenAll(grantTasks).ConfigureAwait(false);
    }
}
