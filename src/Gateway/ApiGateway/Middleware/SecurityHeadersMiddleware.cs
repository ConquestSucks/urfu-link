namespace Urfu.Link.Gateway.ApiGateway.Middleware;

/// <summary>
/// Adds defensive HTTP response headers (no-sniff, frame deny, referrer policy, permissions policy).
/// Cache-Control: no-store is applied to non-hub paths; SignalR transports manage their own caching semantics.
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
                headers["Cache-Control"] = "no-store";
            }

            return Task.CompletedTask;
        }, context);

        await next(context).ConfigureAwait(false);
    }
}
