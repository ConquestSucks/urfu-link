using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace Urfu.Link.Gateway.ApiGateway.Middleware;

/// <summary>
/// Defence-in-depth for SignalR hub routes (<c>/hubs/*</c>): downstream services validate the JWT,
/// but the gateway still requires a token in a known carrier before proxying hub traffic.
/// </summary>
public sealed class HubAccessTokenPresenceMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string HubsPathSegment = "/hubs";
    private const string AccessTokenQueryKey = "access_token";
    private readonly string? _configuredTokenHeader = GetConfiguredTokenHeader(configuration);

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments(HubsPathSegment, StringComparison.Ordinal))
        {
            if (!HasAccessTokenInQuery(context)
                && !HasBearerAuthorizationHeader(context)
                && !HasConfiguredTokenHeader(context))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }

    private static bool HasAccessTokenInQuery(HttpContext context)
    {
        return context.Request.Query.TryGetValue(AccessTokenQueryKey, out var token)
            && !string.IsNullOrWhiteSpace(token);
    }

    private static bool HasBearerAuthorizationHeader(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderNames.Authorization, out var values))
        {
            return false;
        }

        foreach (var raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (AuthenticationHeaderValue.TryParse(raw, out var header)
                && string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(header.Parameter))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasConfiguredTokenHeader(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_configuredTokenHeader))
        {
            return false;
        }

        if (!context.Request.Headers.TryGetValue(_configuredTokenHeader, out var values))
        {
            return false;
        }

        foreach (var raw in values)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetConfiguredTokenHeader(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration["Auth:TokenHeader"];
    }
}
