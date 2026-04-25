using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Realtime;

public sealed record SendMessageHubInput(
    string ConversationId,
    string Body,
    IReadOnlyList<Guid> AttachmentAssetIds,
    string ClientMessageId,
    Guid? ReplyToMessageId = null);

public sealed record EditMessageHubInput(Guid MessageId, string NewBody);

[Authorize]
public sealed class ChatHub(
    OpenDirectConversationService openDirect,
    SendMessageService sendMessage,
    MarkDeliveredService markDelivered,
    MarkReadService markRead,
    EditMessageService editMessage,
    DeleteMessageService deleteMessage,
    ForwardMessagesService forwardMessages) : Hub<IChatClient>
{
    public async Task<ConversationDto> OpenDirectConversation(Guid peerUserId)
    {
        var caller = Context.User!.GetUserId();
        var conversation = await openDirect.OpenAsync(caller, peerUserId, Context.ConnectionAborted).ConfigureAwait(false);
        return ConversationDto.FromDomain(conversation);
    }

    public Task<MessageDto> SendMessage(SendMessageHubInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var caller = Context.User!.GetUserId();
        var assetIds = input.AttachmentAssetIds ?? Array.Empty<Guid>();
        return sendMessage.SendAsync(
            new SendMessageRequest(
                input.ConversationId,
                caller,
                input.Body ?? string.Empty,
                assetIds,
                input.ClientMessageId,
                input.ReplyToMessageId),
            Context.ConnectionAborted);
    }

    public Task<IReadOnlyList<Guid>> MarkDelivered(string conversationId, IReadOnlyList<Guid> messageIds)
    {
        var caller = Context.User!.GetUserId();
        return markDelivered.MarkAsync(
            new MarkDeliveredRequest(conversationId, caller, messageIds ?? Array.Empty<Guid>()),
            Context.ConnectionAborted);
    }

    public Task<Guid?> MarkRead(string conversationId, Guid upToMessageId)
    {
        var caller = Context.User!.GetUserId();
        return markRead.MarkAsync(
            new MarkReadRequest(conversationId, caller, upToMessageId),
            Context.ConnectionAborted);
    }

    public Task<MessageDto> EditMessage(EditMessageHubInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var caller = Context.User!.GetUserId();
        return editMessage.EditAsync(
            new EditMessageRequest(input.MessageId, caller, input.NewBody ?? string.Empty),
            Context.ConnectionAborted);
    }

    public Task<MessageDto?> DeleteMessage(Guid messageId, string mode)
    {
        var caller = Context.User!.GetUserId();
        var deleteMode = ParseMode(mode);
        return deleteMessage.DeleteAsync(
            new DeleteMessageRequest(messageId, caller, deleteMode),
            Context.ConnectionAborted);
    }

    public Task<IReadOnlyList<MessageDto>> ForwardMessages(string targetConversationId, IReadOnlyList<Guid> messageIds)
    {
        var caller = Context.User!.GetUserId();
        return forwardMessages.ForwardAsync(
            new ForwardMessagesRequest(targetConversationId, caller, messageIds ?? Array.Empty<Guid>()),
            Context.ConnectionAborted);
    }

    private static DeleteMode ParseMode(string mode)
    {
        return mode switch
        {
            "for-me" => DeleteMode.ForMe,
            "for-everyone" => DeleteMode.ForEveryone,
            _ => throw new ArgumentException(
                $"Unsupported delete mode '{mode}'. Use 'for-me' or 'for-everyone'.",
                nameof(mode)),
        };
    }
}
