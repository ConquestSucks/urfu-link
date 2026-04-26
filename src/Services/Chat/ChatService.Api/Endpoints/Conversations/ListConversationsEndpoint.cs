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

    /// <summary>
    /// Optional kind filter. Accepted values: <c>direct</c> (only one-to-one chats) or
    /// <c>discipline</c> (only discipline-bound group chats). Anything else is rejected
    /// with a 400 so typos surface during integration rather than silently widening the list.
    /// </summary>
    [QueryParam] public string? Type { get; set; }
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

        if (!TryParseFilter(req.Type, out var filter))
        {
            AddError(r => r.Type!, "Invalid conversation type filter. Allowed: direct, discipline.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
            return;
        }

        try
        {
            var page = await query.ExecuteAsync(caller, req.Cursor, req.Limit, filter, ct).ConfigureAwait(false);
            await Send.OkAsync(page, ct).ConfigureAwait(false);
        }
        catch (InvalidChatCursorException)
        {
            AddError(r => r.Cursor!, "Invalid cursor.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
        }
    }

    private static bool TryParseFilter(string? raw, out ConversationListFilter filter)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            filter = ConversationListFilter.All;
            return true;
        }

        if (string.Equals(raw, "direct", StringComparison.OrdinalIgnoreCase))
        {
            filter = ConversationListFilter.Direct;
            return true;
        }

        if (string.Equals(raw, "discipline", StringComparison.OrdinalIgnoreCase))
        {
            filter = ConversationListFilter.Discipline;
            return true;
        }

        filter = ConversationListFilter.All;
        return false;
    }
}
