using System.Security.Claims;

namespace ChatService.IntegrationTests.Infrastructure;

internal static class TestUserBuilder
{
    public static ClaimsPrincipal Authenticated(Guid userId)
    {
        var identity = new ClaimsIdentity(TestAuthHandler.SchemeName);
        identity.AddClaim(new Claim("sub", userId.ToString("D")));
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")));
        return new ClaimsPrincipal(identity);
    }
}
