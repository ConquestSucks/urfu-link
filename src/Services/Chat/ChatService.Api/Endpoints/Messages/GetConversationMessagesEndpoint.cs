using FastEndpoints;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class GetConversationMessagesEndpoint(GetConversationMessagesQuery query)
    : EndpointWithoutRequest<CursorPage<MessageDto>>
{
    public override void Configure()
    {
        Get("conversations/{id}/messages");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Cursor-paginated message history for a conversation.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = User.GetUserId();
        var id = Route<string>("id")!;
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
        catch (ConversationNotFoundException)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
        }
        catch (ChatAccessDeniedException)
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
        }
    }
}
