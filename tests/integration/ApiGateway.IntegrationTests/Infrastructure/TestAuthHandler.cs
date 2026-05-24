using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiGateway.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal JWT-Bearer-shaped auth scheme for tests.
/// Reads <c>Authorization: Bearer &lt;sub&gt;</c> and produces a principal with <c>sub</c> claim equal to the token.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestBearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = header.ToString();
        const string prefix = "Bearer ";
        if (!token.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var sub = token[prefix.Length..].Trim();
        if (string.IsNullOrEmpty(sub))
        {
            return Task.FromResult(AuthenticateResult.Fail("Empty token"));
        }

        var claims = new[]
        {
            new Claim("sub", sub),
            new Claim(ClaimTypes.NameIdentifier, sub),
            new Claim("sid", $"sid-{sub}"),
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
