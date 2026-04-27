namespace Urfu.Link.Gateway.ApiGateway.Middleware;

/// <summary>
/// Defence-in-depth for SignalR hub routes (<c>/hubs/*</c>): the gateway cannot validate the JWT
/// (downstream services accept it via <c>?access_token=</c> query parameter), but it can require
/// the parameter to be present. Anonymous traffic that did not provide a token at all is rejected
/// here with 401 Unauthorized, before it reaches downstream.
/// </summary>
public sealed class HubAccessTokenPresenceMiddleware(RequestDelegate next)
{
    private const string HubsPathSegment = "/hubs";
    private const string AccessTokenQueryKey = "access_token";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments(HubsPathSegment, StringComparison.Ordinal))
        {
            var hasAccessToken = context.Request.Query.TryGetValue(AccessTokenQueryKey, out var token)
                && !string.IsNullOrWhiteSpace(token);

            if (!hasAccessToken)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers["WWW-Authenticate"] = "Bearer";
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
