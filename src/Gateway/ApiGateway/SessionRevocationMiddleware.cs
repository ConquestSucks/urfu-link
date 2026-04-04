using System.Security.Claims;
using Urfu.Link.BuildingBlocks.SessionRevocation;

namespace Urfu.Link.Gateway.ApiGateway;

public sealed class SessionRevocationMiddleware(
    RequestDelegate next,
    ISessionRevocationStore revocationStore)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirstValue("sub");
            var sid = context.User.FindFirstValue("sid");

            if (sub is not null && sid is not null
                && await revocationStore.IsRevokedAsync(sub, sid, context.RequestAborted).ConfigureAwait(false))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers["X-Session-Revoked"] = "true";
                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
