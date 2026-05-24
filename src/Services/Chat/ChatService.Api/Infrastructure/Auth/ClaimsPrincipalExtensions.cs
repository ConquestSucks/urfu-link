using System.Security.Claims;

namespace Urfu.Link.Services.Chat.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public const string AdminRole = "admin";

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

    /// <summary>
    /// Whether the calling principal carries the realm-level <c>admin</c> role.
    /// Discipline-driven group conversations honour this role globally — admins
    /// can pin/manage messages regardless of their participant status.
    /// </summary>
    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.IsInRole(AdminRole);
    }
}
