using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Conversations;

public sealed class OpenDirectConversationRequest
{
    public Guid PeerUserId { get; set; }
}

public sealed class OpenDirectConversationEndpoint(OpenDirectConversationService service)
    : Endpoint<OpenDirectConversationRequest, ConversationDto>
{
    public override void Configure()
    {
        Post("conversations/direct");
        Group<ChatGroup>();
        Summary(s => s.Summary = "Idempotently open a direct conversation with another user.");
    }

    public override async Task HandleAsync(OpenDirectConversationRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();
        var conversation = await service.OpenAsync(caller, req.PeerUserId, ct).ConfigureAwait(false);
        await Send.OkAsync(ConversationDto.FromDomain(conversation), ct).ConfigureAwait(false);
    }
}
