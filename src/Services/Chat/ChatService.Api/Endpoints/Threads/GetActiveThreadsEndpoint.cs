using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Threads;

public sealed class GetActiveThreadsRequest
{
    [QueryParam] public string? Cursor { get; set; }

    [QueryParam] public int? Limit { get; set; }
}

public sealed class GetActiveThreadsEndpoint(GetUserActiveThreadsQuery query)
    : Endpoint<GetActiveThreadsRequest, CursorPage<ActiveThreadDto>>
{
    public override void Configure()
    {
        Get("threads/active");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Caller's active threads, ordered by last activity desc.");
    }

    public override async Task HandleAsync(GetActiveThreadsRequest req, CancellationToken ct)
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
