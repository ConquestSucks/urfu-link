using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace UserService.Tests.Infrastructure;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    public static ClaimsPrincipal? CurrentPrincipal { get; set; }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (CurrentPrincipal is null)
            return Task.FromResult(AuthenticateResult.Fail("No test principal configured."));

        var ticket = new AuthenticationTicket(CurrentPrincipal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
