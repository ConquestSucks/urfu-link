using Microsoft.Net.Http.Headers;

namespace Urfu.Link.Gateway.ApiGateway.Middleware;

/// <summary>
/// Adds defensive HTTP response headers (no-sniff, frame deny, referrer policy, permissions policy).
/// GET/HEAD responses use revalidation-friendly cache headers so history navigations can use bfcache.
/// HSTS is intentionally not emitted here — it is the responsibility of the edge proxy / ingress.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            var headers = ctx.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            if (!ctx.Request.Path.StartsWithSegments("/hubs", StringComparison.Ordinal))
            {
                headers[HeaderNames.CacheControl] = ShouldAllowBfcacheRevalidation(ctx)
                    ? "private, max-age=0, must-revalidate"
                    : "no-store";
            }

            return Task.CompletedTask;
        }, context);

        await next(context).ConfigureAwait(false);
    }

    private static bool ShouldAllowBfcacheRevalidation(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) &&
            !HttpMethods.IsHead(context.Request.Method))
        {
            return false;
        }

        if (context.Response.StatusCode is < StatusCodes.Status200OK or >= StatusCodes.Status400BadRequest)
        {
            return false;
        }

        return !context.Response.Headers.ContainsKey(HeaderNames.SetCookie);
    }
}
