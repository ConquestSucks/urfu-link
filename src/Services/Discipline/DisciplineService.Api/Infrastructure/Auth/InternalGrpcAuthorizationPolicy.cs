namespace DisciplineService.Api.Infrastructure.Auth;

/// <summary>
/// Names + role allowlist for the gRPC internal-api authorization policy. Lives
/// in one place so the Program.cs wiring and any future filter/test helper agree.
/// </summary>
public static class InternalGrpcAuthorizationPolicy
{
    public const string PolicyName = "DisciplineInternalApi";

    public const string DisciplineReadRole = "service:discipline-read";

    /// <summary>
    /// Roles permitted to call the internal gRPC surface: dedicated service-account
    /// role for sibling services and the global admin override.
    /// </summary>
    public static readonly string[] AllowedRoles =
    [
        DisciplineReadRole,
        ClaimsPrincipalExtensions.AdminRole,
    ];
}
