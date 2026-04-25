using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class GetConversationRequest
{
    public string Id { get; set; } = string.Empty;
}

public sealed class GetConversationEndpoint(GetConversationQuery query)
    : Endpoint<GetConversationRequest, ConversationDto>
{
    public override void Configure()
    {
        Get("conversations/{id}");
        Group<ChatGroup>();
        Summary(s => s.Summary = "Return a single conversation by id.");
    }

    public override async Task HandleAsync(GetConversationRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var dto = await query.ExecuteAsync(req.Id, caller, ct).ConfigureAwait(false);
        await Send.OkAsync(dto, ct).ConfigureAwait(false);
    }
}
