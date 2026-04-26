using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Chat.Application.Contracts;
using Urfu.Link.Services.Chat.Application.Cursors;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

/// <summary>
/// DI key under which the chat-search rate limiter is registered. Endpoints resolve it via
/// <c>[FromKeyedServices]</c> so multiple policies can coexist.
/// </summary>
public static class ChatSearchRateLimiterPolicy
{
    public const string Name = "chat-search";
}

public sealed class SearchMessagesRequest
{
    [QueryParam] public string? Q { get; set; }

    [QueryParam] public string? ConversationId { get; set; }

    [QueryParam] public Guid? SenderId { get; set; }

    [QueryParam] public DateTimeOffset? From { get; set; }

    [QueryParam] public DateTimeOffset? To { get; set; }

    [QueryParam] public bool? HasAttachments { get; set; }

    [QueryParam] public AttachmentType? AttachmentType { get; set; }

    [QueryParam] public string? Cursor { get; set; }

    [QueryParam] public int? Limit { get; set; }
}

public sealed class SearchMessagesValidator : Validator<SearchMessagesRequest>
{
    public SearchMessagesValidator()
    {
        RuleFor(x => x.Q)
            .NotEmpty().WithMessage("Query is required.")
            .MinimumLength(2).WithMessage("Query must be at least 2 characters.");
        RuleFor(x => x.Limit!.Value)
            .InclusiveBetween(1, 100).WithMessage("Limit must be between 1 and 100.")
            .When(x => x.Limit.HasValue);
    }
}

public sealed class SearchMessagesEndpoint(
    SearchMessagesQuery query,
    [FromKeyedServices(ChatSearchRateLimiterPolicy.Name)] IRateLimiter rateLimiter)
    : Endpoint<SearchMessagesRequest, CursorPage<MessageSearchResultDto>>
{
    public override void Configure()
    {
        Get("search");
        Group<Endpoints.ChatGroup>();
        Summary(s => s.Summary = "Full-text search across the caller's chat history.");
    }

    public override async Task HandleAsync(SearchMessagesRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);
        var caller = User.GetUserId();

        var decision = await rateLimiter
            .TryAcquireAsync($"{caller:N}", ct)
            .ConfigureAwait(false);
        if (!decision.Allowed)
        {
            var retry = decision.RetryAfter ?? TimeSpan.FromSeconds(60);
            HttpContext.Response.Headers["Retry-After"] =
                ((int)Math.Ceiling(retry.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            await Send.ResponseAsync(default!, StatusCodes.Status429TooManyRequests, ct).ConfigureAwait(false);
            return;
        }

        try
        {
            var page = await query.ExecuteAsync(
                new SearchMessagesParameters(
                    req.Q!,
                    req.ConversationId,
                    req.SenderId,
                    req.From,
                    req.To,
                    req.HasAttachments,
                    req.AttachmentType,
                    req.Cursor,
                    req.Limit),
                caller,
                ct).ConfigureAwait(false);
            await Send.OkAsync(page, ct).ConfigureAwait(false);
        }
        catch (InvalidChatCursorException)
        {
            AddError(r => r.Cursor!, "Invalid cursor.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct).ConfigureAwait(false);
        }
    }
}
