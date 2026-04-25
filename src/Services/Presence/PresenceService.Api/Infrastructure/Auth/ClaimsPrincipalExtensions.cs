using System.Security.Claims;

namespace Urfu.Link.Services.Presence.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var sub = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT does not contain 'sub' claim.");
        return Guid.Parse(sub);
    }

    public static bool TryGetUserId(this ClaimsPrincipal principal, out Guid userId)
    {
        ArgumentNullException.ThrowIfNull(principal);
        var sub = principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }
}
