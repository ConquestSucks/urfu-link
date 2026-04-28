using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Contracts.Integration.Chat;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Mentions;
using Urfu.Link.Services.Chat.Application.Presence;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure;
using Urfu.Link.Services.Chat.Realtime;

namespace Urfu.Link.Services.Chat.Application.Messages;

/// <summary>
/// Persists a chat message, propagates the conversation preview, validates+grants attachment
/// access via MediaService, and publishes a <c>chat.message.sent.v1</c> integration event.
/// Idempotent on <c>(SenderId, ClientMessageId)</c>: a duplicate request returns the original
/// message without re-persisting or re-publishing.
/// </summary>
public sealed class SendMessageService(
    IConversationRepository conversations,
    IMessageRepository messages,
    IMediaServiceClient mediaServiceClient,
    IIdempotencyStore idempotencyStore,
    ChatEventDispatcher dispatcher,
    IChatBroadcaster broadcaster,
    IPresenceServiceClient presenceClient,
    TimeProvider clock,
    IOptions<ChatOptions> options)
{
    /// <summary>Backwards-compatible alias for <see cref="ChatBodyConstraints.MaxBodyLength"/>.</summary>
    public const int MaxBodyLength = ChatBodyConstraints.MaxBodyLength;

    /// <summary>Backwards-compatible alias for <see cref="ChatBodyConstraints.MaxAttachmentsPerMessage"/>.</summary>
    public const int MaxAttachmentsPerMessage = ChatBodyConstraints.MaxAttachmentsPerMessage;

    public async Task<MessageDto> SendAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ValidatePayloadShape(request);

        var conversation = await conversations.GetByIdAsync(request.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(request.ConversationId);

        if (!conversation.IsParticipant(request.SenderId) && !request.CallerIsAdmin)
        {
            throw new ChatAccessDeniedException(request.ConversationId, request.SenderId);
        }

        if (conversation.IsArchived)
        {
            throw ChatConversationArchivedException.For(conversation.Id);
        }

        if (conversation.IsAnnouncementOnly
            && !conversation.IsTeacher(request.SenderId)
            && !request.CallerIsAdmin)
        {
            throw ChatAnnouncementOnlyException.For(conversation.Id, request.SenderId);
        }

        var idempotencyKey = $"chat:msg:{request.SenderId:N}:{request.ClientMessageId}";
        var firstAttempt = await idempotencyStore.TryRegisterAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (!firstAttempt)
        {
            var prior = await messages.FindByClientMessageIdAsync(request.SenderId, request.ClientMessageId, cancellationToken).ConfigureAwait(false);
            if (prior is not null)
            {
                // The conversation lastMessage update may have failed in the prior attempt — replay
                // it now so the conversation projection eventually catches up. Idempotent at the
                // Mongo write level (Set $set with the same payload).
                await ReprojectLastMessageAsync(conversation, prior, cancellationToken).ConfigureAwait(false);
                return MessageDto.FromDomain(prior);
            }
            // Idempotency win without an actual stored message means the writer crashed before
            // InsertAsync. Fall through and let the unique index reject the duplicate so the
            // caller sees a deterministic error if a competing writer made progress.
        }

        var attachments = await ResolveAttachmentsAsync(request.AttachmentAssetIds, request.SenderId, cancellationToken)
            .ConfigureAwait(false);

        var replyTo = await ResolveReplyToAsync(conversation.Id, request.ReplyToMessageId, cancellationToken)
            .ConfigureAwait(false);

        var opts = options.Value;
        var mentions = MentionsParser.Parse(request.Body, conversation.Participants, opts.MaxMentionsPerMessage);

        var now = clock.GetUtcNow();
        var message = Message.Send(
            id: Guid.NewGuid(),
            conversationId: conversation.Id,
            senderId: request.SenderId,
            body: request.Body,
            attachments: attachments,
            clientMessageId: request.ClientMessageId,
            createdAtUtc: now,
            mentions: mentions,
            replyTo: replyTo,
            authorRole: conversation.RoleOf(request.SenderId));

        try
        {
            await messages.InsertAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateClientMessageException)
        {
            var prior = await messages.FindByClientMessageIdAsync(request.SenderId, request.ClientMessageId, cancellationToken).ConfigureAwait(false);
            if (prior is not null)
            {
                await ReprojectLastMessageAsync(conversation, prior, cancellationToken).ConfigureAwait(false);
                return MessageDto.FromDomain(prior);
            }
            throw;
        }

        var preview = new MessagePreview(request.SenderId, request.Body, now, message.HasAttachments);
        await conversations.UpdateLastMessageAsync(conversation.Id, preview, now, cancellationToken).ConfigureAwait(false);

        await GrantAttachmentAccessAsync(attachments, conversation, request.SenderId, cancellationToken).ConfigureAwait(false);

        var recipients = conversation.Participants.Where(p => p != request.SenderId).ToList();
        await dispatcher.PublishAsync(
            new ChatMessageSentEvent(
                conversation.Id,
                message.Id,
                request.SenderId,
                recipients,
                BuildPreviewText(request.Body, message.HasAttachments),
                message.HasAttachments,
                now,
                Mentions: mentions.Count == 0 ? null : mentions),
            cancellationToken).ConfigureAwait(false);

        if (mentions.Count > 0)
        {
            await dispatcher.PublishAsync(
                new ChatMentionCreatedEvent(
                    conversation.Id,
                    message.Id,
                    request.SenderId,
                    mentions,
                    now),
                cancellationToken).ConfigureAwait(false);
        }

        var dto = MessageDto.FromDomain(message);
        await broadcaster.NotifyMessageReceivedAsync(recipients, dto, cancellationToken).ConfigureAwait(false);

        // Auto-clear the sender's typing indicator on PresenceService — sending a message
        // implies the user is no longer typing, even if their client did not fire the
        // explicit StopTyping. Failures are swallowed by the client (fail-open contract).
        await presenceClient
            .SetTypingAsync(request.ConversationId, request.SenderId, isTyping: false, cancellationToken)
            .ConfigureAwait(false);

        return dto;
    }

    private static void ValidatePayloadShape(SendMessageRequest request)
    {
        if (request.Body is { Length: > MaxBodyLength })
        {
            throw new ChatPayloadTooLargeException(
                $"Message body exceeds {MaxBodyLength} characters.");
        }

        if (request.AttachmentAssetIds is { Count: > MaxAttachmentsPerMessage })
        {
            throw new ChatPayloadTooLargeException(
                $"Message has more than {MaxAttachmentsPerMessage} attachments.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientMessageId))
        {
            throw new ArgumentException("ClientMessageId is required.", nameof(request));
        }
    }

    private async Task<ReplyTo?> ResolveReplyToAsync(
        string conversationId,
        Guid? replyToMessageId,
        CancellationToken cancellationToken)
    {
        if (replyToMessageId is not { } id)
        {
            return null;
        }

        var target = await messages.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (target is null || !string.Equals(target.ConversationId, conversationId, StringComparison.Ordinal))
        {
            throw new ChatReplyTargetNotFoundException(id, conversationId);
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

        // Fan out grants in parallel — each call is a separate gRPC round-trip and the calls
        // are independent.
        var grantTasks = attachments
            .Select(a => mediaServiceClient.GrantConversationAccessAsync(
                a.MediaAssetId, grantees, conversation.Id, senderId, cancellationToken))
            .ToArray();
        await Task.WhenAll(grantTasks).ConfigureAwait(false);
    }

    private async Task ReprojectLastMessageAsync(
        Conversation conversation,
        Message message,
        CancellationToken cancellationToken)
    {
        // Only re-project if THIS message is the one that should be on top of the conversation.
        // A stale write would otherwise overwrite a newer preview.
        if (conversation.LastMessageAtUtc > message.CreatedAtUtc)
        {
            return;
        }

        var preview = new MessagePreview(message.SenderId, message.Body, message.CreatedAtUtc, message.HasAttachments);
        await conversations.UpdateLastMessageAsync(conversation.Id, preview, message.CreatedAtUtc, cancellationToken)
            .ConfigureAwait(false);
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

        // Avoid splitting a UTF-16 surrogate pair when truncating — the high surrogate at
        // index `maxPreview - 1` would otherwise be left without its low surrogate and produce
        // an invalid string when re-encoded.
        var cutoff = char.IsHighSurrogate(body[maxPreview - 1]) ? maxPreview - 1 : maxPreview;
        return body[..cutoff];
    }

}

public sealed class ChatAttachmentNotOwnedException : InvalidOperationException
{
    public ChatAttachmentNotOwnedException()
    {
    }

    public ChatAttachmentNotOwnedException(string message)
        : base(message)
    {
    }

    public ChatAttachmentNotOwnedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ChatAttachmentNotOwnedException(Guid assetId, Guid userId)
        : base($"Asset '{assetId}' is not owned by user '{userId}' or is not in Uploaded state.")
    {
        AssetId = assetId;
        UserId = userId;
    }

    public Guid AssetId { get; }

    public Guid UserId { get; }
}

public sealed class ChatPayloadTooLargeException : InvalidOperationException
{
    public ChatPayloadTooLargeException()
    {
    }

    public ChatPayloadTooLargeException(string message)
        : base(message)
    {
    }

    public ChatPayloadTooLargeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
