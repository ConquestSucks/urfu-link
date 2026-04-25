using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Realtime;

public sealed record SendMessageHubInput(
    string ConversationId,
    string Body,
    IReadOnlyList<AttachmentDto> Attachments,
    string ClientMessageId);

[Authorize]
public sealed class ChatHub(
    OpenDirectConversationService openDirect,
    SendMessageService sendMessage,
    MarkDeliveredService markDelivered,
    MarkReadService markRead) : Hub<IChatClient>
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
        var attachments = input.Attachments?.Select(a => a.ToDomain()).ToList() ?? new List<Attachment>();
        return sendMessage.SendAsync(
            new SendMessageRequest(input.ConversationId, caller, input.Body ?? string.Empty, attachments, input.ClientMessageId),
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
}
