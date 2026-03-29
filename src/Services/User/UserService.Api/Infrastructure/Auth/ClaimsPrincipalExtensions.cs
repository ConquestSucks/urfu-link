using System.Security.Claims;

namespace UserService.Api.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var sub = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT does not contain 'sub' claim.");

        return Guid.Parse(sub);
    }

    public static string GetSessionId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue("sid")
            ?? throw new InvalidOperationException("JWT does not contain 'sid' claim.");
    }
}
