using System.Security.Claims;

namespace Urfu.Link.Services.Call.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var sub = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT does not contain 'sub' claim.");
        return Guid.Parse(sub);
    }
}
