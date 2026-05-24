using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace Urfu.Link.BuildingBlocks.Idempotency;

/// <summary>
/// FastEndpoints / minimal-API filter that consults an <see cref="IRateLimiter"/> before the
/// endpoint runs. On rejection it sets <c>Retry-After</c> and returns 429 directly, skipping
/// the handler. Concrete services derive a one-class-per-policy subclass that supplies the
/// key (e.g. authenticated user id) and the limiter resolved from keyed DI.
/// </summary>
public abstract class RateLimitEndpointFilter(IRateLimiter rateLimiter) : IEndpointFilter
{
    /// <summary>
    /// Returns the rate-limit bucket key for the current request, or <see langword="null"/> /
    /// empty to bypass rate limiting (e.g. for unauthenticated callers when the policy is per
    /// user).
    /// </summary>
    protected abstract string? BuildKey(HttpContext context);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var key = BuildKey(context.HttpContext);
        if (string.IsNullOrEmpty(key))
        {
            return await next(context).ConfigureAwait(false);
        }

        var decision = await rateLimiter
            .TryAcquireAsync(key, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);
        if (decision.Allowed)
        {
            return await next(context).ConfigureAwait(false);
        }

        var retryAfter = (int)Math.Ceiling((decision.RetryAfter ?? TimeSpan.FromMinutes(1)).TotalSeconds);
        context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }
}
