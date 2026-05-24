using FastEndpoints;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class GetConversationEndpoint(GetConversationQuery query)
    : EndpointWithoutRequest<ConversationDto>
{
    public override void Configure()
    {
        Get("conversations/{id}");
        Group<ChatGroup>();
        Summary(s => s.Summary = "Return a single conversation by id.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = User.GetUserId();
        var id = Route<string>("id")!;
        try
        {
            var dto = await query.ExecuteAsync(id, caller, ct).ConfigureAwait(false);
            await Send.OkAsync(dto, ct).ConfigureAwait(false);
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
