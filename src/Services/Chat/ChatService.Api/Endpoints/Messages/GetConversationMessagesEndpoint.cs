using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class GetConversationMessagesRequest
{
    public string Id { get; set; } = string.Empty;

    [QueryParam] public string? Cursor { get; set; }

    [QueryParam] public int? Limit { get; set; }

    [QueryParam] public string? Direction { get; set; }
}

public sealed class GetConversationMessagesEndpoint(GetConversationMessagesQuery query)
    : Endpoint<GetConversationMessagesRequest, CursorPage<MessageDto>>
{
    public override void Configure()
    {
        Get("conversations/{id}/messages");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Cursor-paginated message history for a conversation.");
    }

    public override async Task HandleAsync(GetConversationMessagesRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var direction = string.Equals(req.Direction, "newer", StringComparison.OrdinalIgnoreCase)
            ? CursorDirection.Newer
            : CursorDirection.Older;

        try
        {
            var page = await query.ExecuteAsync(req.Id, caller, req.Cursor, req.Limit, direction, ct).ConfigureAwait(false);
            await Send.OkAsync(page, ct).ConfigureAwait(false);
        }
        catch (InvalidChatCursorException)
        {
            AddError(r => r.Cursor!, "Invalid cursor.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
        }
    }
}
