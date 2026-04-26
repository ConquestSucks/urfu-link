using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class GetThreadMessagesRequest
{
    public Guid Id { get; set; }

    [QueryParam] public string? Cursor { get; set; }

    [QueryParam] public int? Limit { get; set; }

    [QueryParam] public string? Direction { get; set; }
}

public sealed class GetThreadMessagesEndpoint(GetThreadMessagesQuery query)
    : Endpoint<GetThreadMessagesRequest, CursorPage<MessageDto>>
{
    public override void Configure()
    {
        Get("messages/{id}/thread");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Cursor-paginated list of replies in a thread.");
    }

    public override async Task HandleAsync(GetThreadMessagesRequest req, CancellationToken ct)
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
