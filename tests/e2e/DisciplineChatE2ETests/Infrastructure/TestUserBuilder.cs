using System.Security.Claims;

namespace DisciplineChatE2ETests.Infrastructure;

internal static class TestUserBuilder
{
    public static ClaimsPrincipal Authenticated(Guid userId, params string[] roles)
    {
        var identity = new ClaimsIdentity(
            TestAuthHandler.SchemeName,
            nameType: "preferred_username",
            roleType: ClaimTypes.Role);
        identity.AddClaim(new Claim("sub", userId.ToString("D")));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")));

        foreach (var role in roles ?? [])
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
            identity.AddClaim(new Claim("roles", role));
        }

        return new ClaimsPrincipal(identity);
    }

    public static ClaimsPrincipal Admin(Guid userId)
        => Authenticated(userId, "admin");

    public static ClaimsPrincipal Teacher(Guid userId)
        => Authenticated(userId, "teacher");

    public static ClaimsPrincipal Student(Guid userId)
        => Authenticated(userId, "student");
}
