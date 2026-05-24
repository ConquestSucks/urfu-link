using System.Security.Claims;

namespace PresenceService.IntegrationTests.Infrastructure;

public static class TestUserBuilder
{
    public static ClaimsPrincipal MakeUser(Guid userId, string? deviceId = null, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("preferred_username", $"user-{userId:N}"),
        };
        if (!string.IsNullOrEmpty(deviceId))
        {
            claims.Add(new Claim("device_id", deviceId));
        }
        foreach (var role in roles ?? [])
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("groups", role));
        }

        var identity = new ClaimsIdentity(claims, authenticationType: TestAuthHandler.SchemeName);
        return new ClaimsPrincipal(identity);
    }
}
