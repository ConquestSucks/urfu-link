using System.Globalization;
using Grpc.Core;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Grpc;

namespace Urfu.Link.Services.Chat.Services;

public sealed class InternalApiService(IConversationRepository conversations) : InternalApi.InternalApiBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        _ = context;
        return Task.FromResult(new PingReply
        {
            Message = string.IsNullOrWhiteSpace(request.Message) ? "pong" : $"pong:{request.Message}",
            Service = "chat-service",
            Utc = DateTimeOffset.UtcNow.ToString("O"),
        });
    }

    public override async Task<GetConversationParticipantsReply> GetConversationParticipants(
        GetConversationParticipantsRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conv = await conversations.GetByIdAsync(request.ConversationId, context.CancellationToken).ConfigureAwait(false);
        var reply = new GetConversationParticipantsReply();
        if (conv is not null)
        {
            reply.UserIds.AddRange(conv.Participants.Select(p => p.ToString("D", CultureInfo.InvariantCulture)));
        }
        return reply;
    }

    public override async Task<IsParticipantReply> IsParticipant(
        IsParticipantRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conv = await conversations.GetByIdAsync(request.ConversationId, context.CancellationToken).ConfigureAwait(false);
        if (conv is null)
        {
            return new IsParticipantReply { Participates = false, ConversationExists = false };
        }

        if (!Guid.TryParse(request.UserId, out var userId))
        {
            return new IsParticipantReply { Participates = false, ConversationExists = true };
        }

        return new IsParticipantReply
        {
            Participates = conv.IsParticipant(userId),
            ConversationExists = true,
        };
    }

    public override async Task<GetConversationReply> GetConversation(
        GetConversationRequest request,
        ServerCallContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        var conv = await conversations.GetByIdAsync(request.ConversationId, context.CancellationToken).ConfigureAwait(false);
        if (conv is null)
        {
            return new GetConversationReply { Exists = false };
        }

        var reply = new GetConversationReply
        {
            Exists = true,
            Type = ConversationKind.Direct,
        };
        reply.Participants.AddRange(conv.Participants.Select(p => p.ToString("D", CultureInfo.InvariantCulture)));
        return reply;
    }
}
