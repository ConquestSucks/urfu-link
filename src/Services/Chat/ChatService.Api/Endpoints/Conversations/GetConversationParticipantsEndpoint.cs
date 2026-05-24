using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class GetConversationParticipantsEndpoint(GetConversationParticipantsQuery query)
    : EndpointWithoutRequest<IReadOnlyList<ConversationParticipantDto>>
{
    public override void Configure()
    {
        Get("conversations/{id}/participants");
        Group<ChatGroup>();
        Summary(s => s.Summary =
            "Return the participants of a conversation along with their roles. " +
            "Caller must be a participant or an admin.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = User.GetUserId();
        var isAdmin = User.IsAdmin();
        var id = Route<string>("id")!;
        var participants = await query
            .ExecuteAsync(id, caller, isAdmin, ct)
            .ConfigureAwait(false);
        await Send.OkAsync(participants, ct).ConfigureAwait(false);
    }
}
