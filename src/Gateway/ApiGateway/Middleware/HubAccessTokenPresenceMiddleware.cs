using System.Net.Http.Headers;
using Microsoft.Net.Http.Headers;

namespace Urfu.Link.Gateway.ApiGateway.Middleware;

/// <summary>
/// Defence-in-depth for SignalR hub routes (<c>/hubs/*</c>): the gateway cannot validate the JWT
/// (downstream services accept it via <c>?access_token=</c> query parameter or via the
/// <c>Authorization: Bearer</c> header), but it can require that a token be present in one of those
/// two channels. Anonymous traffic that did not provide a token at all is rejected here with
/// 401 Unauthorized, before it reaches downstream.
/// </summary>
/// <remarks>
/// SignalR JS-клиент использует разные каналы передачи токена в зависимости от транспорта:
/// на фазе HTTP-negotiate он шлёт <c>Authorization: Bearer</c>, при апгрейде в WebSocket
/// токен переезжает в <c>?access_token=</c> query parameter. Middleware принимает оба варианта,
/// чтобы валидный запрос не отвергался до того, как стандартный JWT-pipeline проверит подпись.
/// </remarks>
public sealed class HubAccessTokenPresenceMiddleware(RequestDelegate next)
{
    private const string HubsPathSegment = "/hubs";
    private const string AccessTokenQueryKey = "access_token";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments(HubsPathSegment, StringComparison.Ordinal))
        {
            if (!HasAccessTokenInQuery(context) && !HasBearerAuthorizationHeader(context))
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
}
