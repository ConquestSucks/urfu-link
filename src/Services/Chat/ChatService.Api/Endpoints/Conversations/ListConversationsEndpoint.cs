using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class ListConversationsRequest
{
    [QueryParam] public string? Cursor { get; set; }

    [QueryParam] public int? Limit { get; set; }
}

public sealed class ListConversationsEndpoint(GetUserConversationsQuery query)
    : Endpoint<ListConversationsRequest, CursorPage<ConversationDto>>
{
    public override void Configure()
    {
        Get("conversations");
        Group<ChatGroup>();
        Summary(s => s.Summary = "List conversations of the caller, ordered by most recent message.");
    }

    public override async Task HandleAsync(ListConversationsRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        try
        {
            var page = await query.ExecuteAsync(caller, req.Cursor, req.Limit, ct).ConfigureAwait(false);
            await Send.OkAsync(page, ct).ConfigureAwait(false);
        }
        catch (InvalidChatCursorException)
        {
            AddError(r => r.Cursor!, "Invalid cursor.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
        }
    }
}
