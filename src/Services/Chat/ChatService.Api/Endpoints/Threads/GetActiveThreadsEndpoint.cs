using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Threads;

public sealed class GetActiveThreadsEndpoint(GetUserActiveThreadsQuery query)
    : EndpointWithoutRequest<CursorPage<ActiveThreadDto>>
{
    public override void Configure()
    {
        Get("threads/active");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Caller's active threads, ordered by last activity desc.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = User.GetUserId();
        var cursor = Query<string?>("cursor", isRequired: false);
        var limit = Query<int?>("limit", isRequired: false);

        try
        {
            var page = await query.ExecuteAsync(caller, cursor, limit, ct).ConfigureAwait(false);
            await Send.OkAsync(page, ct).ConfigureAwait(false);
        }
        catch (InvalidChatCursorException)
        {
            AddError("cursor", "Invalid cursor.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
        }
    }
}
