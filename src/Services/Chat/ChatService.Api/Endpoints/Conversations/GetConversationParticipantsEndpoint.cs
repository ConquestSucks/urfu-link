using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class GetConversationParticipantsRequest
{
    public string Id { get; set; } = string.Empty;
}

public sealed class GetConversationParticipantsEndpoint(GetConversationParticipantsQuery query)
    : Endpoint<GetConversationParticipantsRequest, IReadOnlyList<ConversationParticipantDto>>
{
    public override void Configure()
    {
        Get("conversations/{id}/participants");
        Group<ChatGroup>();
        Summary(s => s.Summary =
            "Return the participants of a conversation along with their roles. " +
            "Caller must be a participant or an admin.");
    }

    public override async Task HandleAsync(GetConversationParticipantsRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var isAdmin = User.IsAdmin();
        var participants = await query
            .ExecuteAsync(req.Id, caller, isAdmin, ct)
            .ConfigureAwait(false);
        await Send.OkAsync(participants, ct).ConfigureAwait(false);
    }
}
