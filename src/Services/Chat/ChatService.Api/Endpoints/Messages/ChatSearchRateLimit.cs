using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Idempotency;
using Urfu.Link.Services.Chat.Infrastructure.Auth;

namespace Urfu.Link.Services.Chat.Endpoints.Messages;

/// <summary>
/// DI key under which the <c>/chat/search</c> per-user rate limiter is registered.
/// </summary>
public static class ChatSearchRateLimiterPolicy
{
    public const string Name = "chat-search";
}

/// <summary>
/// Per-authenticated-user rate-limit filter for <see cref="SearchMessagesEndpoint"/>. Resolves
/// the policy-specific limiter from keyed DI and keys the bucket by the caller's user id.
/// Unauthenticated requests bypass the filter (the endpoint group already requires auth, this
/// is a defensive fallback).
/// </summary>
public sealed class ChatSearchRateLimitFilter(
    [FromKeyedServices(ChatSearchRateLimiterPolicy.Name)] IRateLimiter rateLimiter)
    : RateLimitEndpointFilter(rateLimiter)
{
    protected override string? BuildKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.User.Identity?.IsAuthenticated == true
            ? context.User.GetUserId().ToString("N")
            : null;
    }
}
