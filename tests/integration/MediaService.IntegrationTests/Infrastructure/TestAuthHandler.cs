using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MediaService.IntegrationTests.Infrastructure;

/// <summary>
/// Minimal auth handler for integration tests — short-circuits the JWT pipeline
/// and authenticates against <see cref="CurrentPrincipal"/>, which the test
/// sets up before issuing the HTTP request.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

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

        var ticket = new AuthenticationTicket(CurrentPrincipal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
