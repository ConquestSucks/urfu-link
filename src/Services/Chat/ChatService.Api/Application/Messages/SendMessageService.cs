using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
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
    TimeProvider clock)
{
    public async Task<MessageDto> SendAsync(SendMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var conversation = await conversations.GetByIdAsync(request.ConversationId, cancellationToken).ConfigureAwait(false)
            ?? throw ConversationNotFoundException.For(request.ConversationId);

        if (!conversation.IsParticipant(request.SenderId))
        {
            throw new ChatAccessDeniedException(request.ConversationId, request.SenderId);
        }

        var idempotencyKey = $"chat:msg:{request.SenderId:N}:{request.ClientMessageId}";
        var firstAttempt = await idempotencyStore.TryRegisterAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
        if (!firstAttempt)
        {
            var prior = await messages.FindByClientMessageIdAsync(request.SenderId, request.ClientMessageId, cancellationToken).ConfigureAwait(false);
            if (prior is not null)
            {
                return MessageDto.FromDomain(prior);
            }
            // Idempotency win without an actual stored message means the writer crashed mid-way
            // last time. Fall through and let the unique index reject the duplicate so the caller
            // sees a deterministic error.
        }

        await ValidateAttachmentsOwnershipAsync(request.Attachments, request.SenderId, cancellationToken).ConfigureAwait(false);

        var now = clock.GetUtcNow();
        var message = Message.Send(
            id: Guid.NewGuid(),
            conversationId: conversation.Id,
            senderId: request.SenderId,
            body: request.Body,
            attachments: request.Attachments,
            clientMessageId: request.ClientMessageId,
            createdAtUtc: now);

        try
        {
            await messages.InsertAsync(message, cancellationToken).ConfigureAwait(false);
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

        var preview = new MessagePreview(request.SenderId, request.Body, now, message.HasAttachments);
        await conversations.UpdateLastMessageAsync(conversation.Id, preview, now, cancellationToken).ConfigureAwait(false);

        await GrantAttachmentAccessAsync(request, conversation, cancellationToken).ConfigureAwait(false);

        var recipients = conversation.Participants.Where(p => p != request.SenderId).ToList();
        await dispatcher.PublishAsync(
            new ChatMessageSentEvent(
                conversation.Id,
                message.Id,
                request.SenderId,
                recipients,
                BuildPreviewText(request.Body, message.HasAttachments),
                message.HasAttachments,
                now),
            cancellationToken).ConfigureAwait(false);

        var dto = MessageDto.FromDomain(message);
        await broadcaster.NotifyMessageReceivedAsync(recipients, dto, cancellationToken).ConfigureAwait(false);

        return dto;
    }

    private async Task ValidateAttachmentsOwnershipAsync(
        IReadOnlyList<Attachment> attachments,
        Guid senderId,
        CancellationToken cancellationToken)
    {
        foreach (var attachment in attachments)
        {
            var owns = await mediaServiceClient.CheckOwnershipAsync(attachment.MediaAssetId, senderId, cancellationToken).ConfigureAwait(false);
            if (!owns)
            {
                throw new ChatAttachmentNotOwnedException(attachment.MediaAssetId, senderId);
            }
        }
    }

    private async Task GrantAttachmentAccessAsync(
        SendMessageRequest request,
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        if (request.Attachments.Count == 0)
        {
            return;
        }

        var grantees = conversation.Participants.Where(p => p != request.SenderId).ToList();
        if (grantees.Count == 0)
        {
            return;
        }

        foreach (var attachment in request.Attachments)
        {
            await mediaServiceClient.GrantConversationAccessAsync(
                attachment.MediaAssetId,
                grantees,
                conversation.Id,
                request.SenderId,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildPreviewText(string body, bool hasAttachments)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return hasAttachments ? "[attachment]" : string.Empty;
        }

        const int maxPreview = 120;
        return body.Length <= maxPreview ? body : body[..maxPreview];
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
        : base($"Asset '{assetId}' is not owned by user '{userId}'.")
    {
        AssetId = assetId;
        UserId = userId;
    }

    public Guid AssetId { get; }

    public Guid UserId { get; }
}
