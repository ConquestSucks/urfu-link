using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

// GET-эндпоинт без тела. FastEndpoints с базовым классом Endpoint<TReq, TResp>
// всё равно пытается JSON-десериализовать Request.Body, поэтому используем
// EndpointWithoutRequest<TResp> и читаем query вручную через Query<T>().
public sealed class ListConversationsEndpoint(GetUserConversationsQuery query)
    : EndpointWithoutRequest<CursorPage<ConversationDto>>
{
    public override void Configure()
    {
        Get("conversations");
        Group<ChatGroup>();
        Summary(s => s.Summary = "List conversations of the caller, ordered by most recent message.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = User.GetUserId();
        var cursor = Query<string?>("cursor", isRequired: false);
        var limit = Query<int?>("limit", isRequired: false);
        var typeRaw = Query<string?>("type", isRequired: false);

        if (!TryParseFilter(typeRaw, out var filter))
        {
            AddError("type", "Invalid conversation type filter. Allowed: direct, discipline.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
            return;
        }

        try
        {
            var page = await query.ExecuteAsync(caller, cursor, limit, filter, ct).ConfigureAwait(false);
            await Send.OkAsync(page, ct).ConfigureAwait(false);
        }
        catch (InvalidChatCursorException)
        {
            AddError("cursor", "Invalid cursor.");
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
