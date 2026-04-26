using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DisciplineService.IntegrationTests.Infrastructure;

public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    // Static field; the integration suite uses a single xUnit collection (sequential
    // by default) so cross-test interference does not occur. AsyncLocal was tried but
    // does not flow reliably through the in-memory test pipeline once
    // WebApplicationFactory captures request scopes outside the test's logical-call
    // context — the request handler then sees a null principal and returns 401.
    public static ClaimsPrincipal? CurrentPrincipal { get; set; }

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (CurrentPrincipal is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var ticket = new AuthenticationTicket(CurrentPrincipal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
