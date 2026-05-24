using FastEndpoints;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

public sealed class SearchMessagesEndpoint(SearchMessagesQuery query)
    : EndpointWithoutRequest<CursorPage<MessageSearchResultDto>>
{
    public override void Configure()
    {
        Get("search");
        Group<Endpoints.ChatGroup>();
        Options(x => x.AddEndpointFilter<ChatSearchRateLimitFilter>());
        Summary(s => s.Summary = "Full-text search across the caller's chat history.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var caller = User.GetUserId();

        var q = Query<string?>("q", isRequired: false);
        var conversationId = Query<string?>("conversationId", isRequired: false);
        var senderId = Query<Guid?>("senderId", isRequired: false);
        var from = Query<DateTimeOffset?>("from", isRequired: false);
        var to = Query<DateTimeOffset?>("to", isRequired: false);
        var hasAttachments = Query<bool?>("hasAttachments", isRequired: false);
        var attachmentType = Query<AttachmentType?>("attachmentType", isRequired: false);
        var cursor = Query<string?>("cursor", isRequired: false);
        var limit = Query<int?>("limit", isRequired: false);

        // Ручная валидация: при EndpointWithoutRequest FluentValidation не применяется,
        // поэтому проверки переносим сюда. Логика идентична прежнему SearchMessagesValidator.
        if (string.IsNullOrWhiteSpace(q))
        {
            AddError("q", "Query is required.");
        }
        else if (q.Length < 2)
        {
            AddError("q", "Query must be at least 2 characters.");
        }

        if (limit.HasValue && (limit.Value < 1 || limit.Value > 100))
        {
            AddError("limit", "Limit must be between 1 and 100.");
        }

        // hasAttachments=false и attachmentType=X — взаимоисключающие;
        // молчаливый пустой ответ скрыл бы баг клиента.
        if (hasAttachments == false && attachmentType.HasValue)
        {
            AddError("attachmentType", "attachmentType cannot be combined with hasAttachments=false.");
        }

        ThrowIfAnyErrors(StatusCodes.Status400BadRequest);

        try
        {
            var page = await query.ExecuteAsync(
                new SearchMessagesParameters(
                    q!,
                    conversationId,
                    senderId,
                    from,
                    to,
                    hasAttachments,
                    attachmentType,
                    cursor,
                    limit),
                caller,
                ct).ConfigureAwait(false);
            await Send.OkAsync(page, ct).ConfigureAwait(false);
        }
        catch (InvalidChatCursorException)
        {
            AddError("cursor", "Invalid cursor.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
        }
    }
}
