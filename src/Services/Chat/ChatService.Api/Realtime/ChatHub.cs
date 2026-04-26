using System.Globalization;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Auth;
// PinMessageService and UnpinMessageService live in Application.Conversations.

namespace Urfu.Link.Services.Chat.Realtime;

public sealed record SendMessageHubInput(
    string ConversationId,
    string Body,
    IReadOnlyList<Guid> AttachmentAssetIds,
    string ClientMessageId,
    Guid? ReplyToMessageId = null);

public sealed record EditMessageHubInput(Guid MessageId, string NewBody);

public sealed record ReplyInThreadHubInput(
    Guid RootMessageId,
    string Body,
    IReadOnlyList<Guid> AttachmentAssetIds,
    string ClientMessageId,
    Guid? ReplyToMessageId = null);

[Authorize]
public sealed class ChatHub(
    OpenDirectConversationService openDirect,
    SendMessageService sendMessage,
    MarkDeliveredService markDelivered,
    MarkReadService markRead,
    EditMessageService editMessage,
    DeleteMessageService deleteMessage,
    ForwardMessagesService forwardMessages,
    AddReactionService addReaction,
    RemoveReactionService removeReaction,
    PinMessageService pinMessage,
    UnpinMessageService unpinMessage,
    ReplyInThreadService replyInThread,
    JoinThreadService joinThread,
    LeaveThreadService leaveThread,
    GetThreadMessagesQuery getThreadMessages,
    IConversationRepository conversationRepository,
    IDisciplineServiceClient disciplineServiceClient,
    ILogger<ChatHub> logger) : Hub<IChatClient>
{
    /// <summary>
    /// SignalR group name for a conversation. Connections are added to this group on
    /// <see cref="OnConnectedAsync"/>; broadcasts addressed at <c>Clients.Group(name)</c>
    /// reach every active connection a participant has open.
    /// </summary>
    internal static string GroupNameFor(string conversationId) => $"conv:{conversationId}";

    /// <summary>
    /// Called by SignalR right after the JWT-authenticated handshake. Joins the connection
    /// to a group per conversation the user can see — discipline groups via the upstream
    /// gRPC source-of-truth and direct chats via the local Mongo projection. The two paths
    /// are merged so a connection ends up in a group even if a UserEnrolled event has not
    /// been consumed yet (gRPC catches the gap).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var principal = Context.User;
        if (principal is null)
        {
            await base.OnConnectedAsync().ConfigureAwait(false);
            return;
        }

        var userId = principal.GetUserId();
        var ct = Context.ConnectionAborted;
        var conversationIds = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            var disciplines = await disciplineServiceClient
                .ListUserDisciplinesAsync(userId, ct)
                .ConfigureAwait(false);
            foreach (var d in disciplines)
            {
                conversationIds.Add($"discipline:{d.DisciplineId.ToString("N", CultureInfo.InvariantCulture)}");
            }
        }
        catch (RpcException ex)
        {
            // DisciplineService gRPC outage shouldn't fail the SignalR handshake. The Mongo
            // projection below still gives the user every conversation they were already in.
            logger.LogWarning(
                ex,
                "DisciplineService gRPC ListUserDisciplines failed for {UserId}; falling back to local projection only.",
                userId);
        }

        try
        {
            var localIds = await conversationRepository
                .GetUserConversationIdsAsync(userId, ct)
                .ConfigureAwait(false);
            foreach (var id in localIds)
            {
                conversationIds.Add(id);
            }
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            // Mongo outage shouldn't fail the SignalR handshake — the gRPC pass above already
            // covered the user's discipline groups, and broadcasts targeted via Clients.Users
            // still reach the connection without group membership.
            logger.LogWarning(
                ex,
                "Failed to load local conversation ids for {UserId}; some SignalR groups may not be joined.",
                userId);
        }

        foreach (var convId in conversationIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(convId), ct).ConfigureAwait(false);
        }

        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public async Task<ConversationDto> OpenDirectConversation(Guid peerUserId)
    {
        var caller = Context.User!.GetUserId();
        var conversation = await openDirect.OpenAsync(caller, peerUserId, Context.ConnectionAborted).ConfigureAwait(false);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(conversation.Id), Context.ConnectionAborted)
            .ConfigureAwait(false);
        return ConversationDto.FromDomain(conversation);
    }

    public Task<MessageDto> SendMessage(SendMessageHubInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var principal = Context.User!;
        var caller = principal.GetUserId();
        var assetIds = input.AttachmentAssetIds ?? Array.Empty<Guid>();
        return sendMessage.SendAsync(
            new SendMessageRequest(
                input.ConversationId,
                caller,
                input.Body ?? string.Empty,
                assetIds,
                input.ClientMessageId,
                input.ReplyToMessageId,
                principal.IsAdmin()),
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
        var principal = Context.User!;
        var caller = principal.GetUserId();
        var deleteMode = DeleteModes.Parse(mode);
        return deleteMessage.DeleteAsync(
            new DeleteMessageRequest(messageId, caller, deleteMode, principal.IsAdmin()),
            Context.ConnectionAborted);
    }

    public Task<IReadOnlyList<MessageDto>> ForwardMessages(string targetConversationId, IReadOnlyList<Guid> messageIds)
    {
        var caller = Context.User!.GetUserId();
        return forwardMessages.ForwardAsync(
            new ForwardMessagesRequest(targetConversationId, caller, messageIds ?? Array.Empty<Guid>()),
            Context.ConnectionAborted);
    }

    public Task AddReaction(Guid messageId, string emoji)
    {
        var caller = Context.User!.GetUserId();
        return addReaction.AddAsync(
            new AddReactionRequest(messageId, caller, emoji ?? string.Empty),
            Context.ConnectionAborted);
    }

    public Task RemoveReaction(Guid messageId, string emoji)
    {
        var caller = Context.User!.GetUserId();
        return removeReaction.RemoveAsync(
            new RemoveReactionRequest(messageId, caller, emoji ?? string.Empty),
            Context.ConnectionAborted);
    }

    public Task<IReadOnlyList<MessageDto>> PinMessage(string conversationId, Guid messageId)
    {
        var principal = Context.User!;
        var caller = principal.GetUserId();
        return pinMessage.PinAsync(
            new PinMessageRequest(conversationId, caller, principal.IsAdmin(), messageId),
            Context.ConnectionAborted);
    }

    public Task<IReadOnlyList<MessageDto>> UnpinMessage(string conversationId, Guid messageId)
    {
        var principal = Context.User!;
        var caller = principal.GetUserId();
        return unpinMessage.UnpinAsync(
            new UnpinMessageRequest(conversationId, caller, principal.IsAdmin(), messageId),
            Context.ConnectionAborted);
    }

    public Task<MessageDto> ReplyInThread(ReplyInThreadHubInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var caller = Context.User!.GetUserId();
        return replyInThread.ReplyAsync(
            new ReplyInThreadRequest(
                caller,
                input.RootMessageId,
                input.Body ?? string.Empty,
                input.AttachmentAssetIds ?? Array.Empty<Guid>(),
                input.ReplyToMessageId,
                input.ClientMessageId),
            Context.ConnectionAborted);
    }

    public Task JoinThread(Guid rootMessageId)
    {
        var caller = Context.User!.GetUserId();
        return joinThread.JoinAsync(new JoinThreadRequest(caller, rootMessageId), Context.ConnectionAborted);
    }

    public Task LeaveThread(Guid rootMessageId)
    {
        var caller = Context.User!.GetUserId();
        return leaveThread.LeaveAsync(new LeaveThreadRequest(caller, rootMessageId), Context.ConnectionAborted);
    }

    /// <summary>
    /// Walks the thread reply list. Defaults to <see cref="CursorDirection.Older"/> so an empty
    /// cursor returns the most recent replies first — matching the main-flow REST contract.
    /// </summary>
    public Task<CursorPage<MessageDto>> GetThreadMessages(Guid rootMessageId, string? cursor, int? limit)
    {
        var caller = Context.User!.GetUserId();
        return getThreadMessages.ExecuteAsync(
            rootMessageId, caller, cursor, limit, CursorDirection.Older, Context.ConnectionAborted);
    }
}
