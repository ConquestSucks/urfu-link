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

    public static string GetDisplayName(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue("name")
            ?? principal.FindFirstValue("preferred_username")
            ?? string.Empty;
    }

    public static string GetEmail(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue("email") ?? string.Empty;
    }

    public static string GetUsername(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindFirstValue("preferred_username") ?? string.Empty;
    }
}
