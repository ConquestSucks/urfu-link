using System.Security.Claims;
using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Infrastructure.Auth;

namespace DisciplineService.Api.Application.Authorization;

/// <summary>
/// Centralizes the rules for who can read or modify a discipline. Admin can do
/// anything; the owner teacher and any teacher enrolled in the discipline can
/// modify it; enrolled users (any role) can read it.
/// </summary>
public sealed class DisciplineAuthorizationService
{
    public bool CanCreate(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.IsAdmin();
    }

    public bool CanModify(ClaimsPrincipal principal, Discipline discipline)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(discipline);

        if (principal.IsAdmin())
        {
            return true;
        }

        if (!principal.TryGetUserId(out var userId))
        {
            return false;
        }

        return discipline.OwnerTeacherId == userId || discipline.IsTeacher(userId);
    }

    public bool CanDelete(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.IsAdmin();
    }

    public bool CanRead(ClaimsPrincipal principal, Discipline discipline)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(discipline);

        if (principal.IsAdmin())
        {
            return true;
        }

        if (!principal.TryGetUserId(out var userId))
        {
            return false;
        }

        return discipline.HasMember(userId);
    }
}
