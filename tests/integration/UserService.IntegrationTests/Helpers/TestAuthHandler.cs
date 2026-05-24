using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UserService.IntegrationTests.Helpers;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";
    public const string DefaultUserId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    public const string DefaultSessionId = "test-session-001";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue("X-Test-UserId", out var userIdHeader)
            ? userIdHeader.ToString()
            : DefaultUserId;

        var sessionId = Request.Headers.TryGetValue("X-Test-SessionId", out var sessionIdHeader)
            ? sessionIdHeader.ToString()
            : DefaultSessionId;

        var username = Request.Headers.TryGetValue("X-Test-Username", out var usernameHeader)
            ? usernameHeader.ToString()
            : "test-user";

        var claims = new List<Claim>
        {
            new("sub", userId),
            new("sid", sessionId),
            new("preferred_username", username),
        };

        if (Request.Headers.TryGetValue("X-Test-Name", out var nameHeader))
        {
            claims.Add(new Claim("name", nameHeader.ToString()));
        }

        if (Request.Headers.TryGetValue("X-Test-Email", out var emailHeader))
        {
            claims.Add(new Claim("email", emailHeader.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
