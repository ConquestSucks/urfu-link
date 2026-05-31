using System.Security.Claims;
using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Infrastructure.Auth;

namespace DisciplineService.Api.Application.Authorization;

/// <summary>
/// Centralizes the rules for who can read a discipline. Discipline, subgroup,
/// and enrollment records are maintained outside the public API.
/// </summary>
public sealed class DisciplineAuthorizationService
{
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
