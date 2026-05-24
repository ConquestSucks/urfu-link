using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Threads;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class GetThreadMessagesEndpoint(GetThreadMessagesQuery query)
    : EndpointWithoutRequest<CursorPage<MessageDto>>
{
    public override void Configure()
    {
        Get("messages/{id}/thread");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Cursor-paginated list of replies in a thread.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = User.GetUserId();
        var id = Route<Guid>("id");
        var cursor = Query<string?>("cursor", isRequired: false);
        var limit = Query<int?>("limit", isRequired: false);
        var directionRaw = Query<string?>("direction", isRequired: false);
        var direction = string.Equals(directionRaw, "newer", StringComparison.OrdinalIgnoreCase)
            ? CursorDirection.Newer
            : CursorDirection.Older;

        try
        {
            var page = await query.ExecuteAsync(id, caller, cursor, limit, direction, ct).ConfigureAwait(false);
            await Send.OkAsync(page, ct).ConfigureAwait(false);
        }
        catch (InvalidChatCursorException)
        {
            AddError("cursor", "Invalid cursor.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
        }
    }
}
