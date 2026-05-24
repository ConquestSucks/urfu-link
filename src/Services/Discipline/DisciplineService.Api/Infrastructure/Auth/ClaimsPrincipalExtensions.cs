using System.Security.Claims;

namespace DisciplineService.Api.Infrastructure.Auth;

public static class ClaimsPrincipalExtensions
{
    public const string AdminRole = "admin";
    public const string TeacherRole = "teacher";
    public const string StudentRole = "student";

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
        if (Guid.TryParse(sub, out userId))
        {
            return true;
        }

        userId = Guid.Empty;
        return false;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.IsInRole(AdminRole);
    }

    public static bool IsTeacher(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.IsInRole(TeacherRole);
    }

    public static bool IsStudent(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.IsInRole(StudentRole);
    }
}
