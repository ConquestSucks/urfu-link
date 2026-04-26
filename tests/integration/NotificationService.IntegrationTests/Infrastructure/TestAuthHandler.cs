using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NotificationService.IntegrationTests.Infrastructure;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    public static ClaimsPrincipal? CurrentPrincipal { get; set; }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (CurrentPrincipal is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var ticket = new AuthenticationTicket(CurrentPrincipal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    public static ClaimsPrincipal Principal(Guid userId)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", userId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            },
            SchemeName);
        return new ClaimsPrincipal(identity);
    }
}
