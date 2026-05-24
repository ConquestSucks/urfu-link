using System.Security.Claims;
using UserService.Api.Infrastructure.Auth;

namespace UserService.UnitTests.Infrastructure;

public sealed class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal MakePrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Type, c.Value)),
            authenticationType: "test");

        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void GetUserIdReturnsSubClaimAsGuid()
    {
        var userId = Guid.NewGuid();
        var principal = MakePrincipal(("sub", userId.ToString()));

        var result = principal.GetUserId();

        Assert.Equal(userId, result);
    }

    [Fact]
    public void GetUserIdThrowsWhenSubClaimIsMissing()
    {
        var principal = MakePrincipal();

        var exception = Assert.Throws<InvalidOperationException>(() => principal.GetUserId());

        Assert.Equal("JWT does not contain 'sub' claim.", exception.Message);
    }

    [Fact]
    public void GetSessionIdReturnsSidClaim()
    {
        var principal = MakePrincipal(("sid", "session-abc"));

        var result = principal.GetSessionId();

        Assert.Equal("session-abc", result);
    }

    [Fact]
    public void GetSessionIdThrowsWhenSidClaimIsMissing()
    {
        var principal = MakePrincipal();

        var exception = Assert.Throws<InvalidOperationException>(() => principal.GetSessionId());

        Assert.Equal("JWT does not contain 'sid' claim.", exception.Message);
    }

    [Fact]
    public void TryGetSessionIdReturnsNullWhenSidClaimIsMissing()
    {
        var principal = MakePrincipal();

        var result = principal.TryGetSessionId();

        Assert.Null(result);
    }

    [Fact]
    public void GetDisplayNamePrefersNameClaim()
    {
        var principal = MakePrincipal(
            ("name", "Nikita Baranov"),
            ("preferred_username", "n.baranov"));

        var result = principal.GetDisplayName();

        Assert.Equal("Nikita Baranov", result);
    }

    [Fact]
    public void GetDisplayNameFallsBackToUsername()
    {
        var principal = MakePrincipal(("preferred_username", "n.baranov"));

        var result = principal.GetDisplayName();

        Assert.Equal("n.baranov", result);
    }

    [Fact]
    public void GetDisplayNameReturnsEmptyWhenNameClaimsAreMissing()
    {
        var principal = MakePrincipal();

        var result = principal.GetDisplayName();

        Assert.Empty(result);
    }

    [Fact]
    public void GetEmailReturnsEmailClaim()
    {
        var principal = MakePrincipal(("email", "n.baranov@urfu.me"));

        var result = principal.GetEmail();

        Assert.Equal("n.baranov@urfu.me", result);
    }

    [Fact]
    public void GetEmailReturnsEmptyWhenEmailClaimIsMissing()
    {
        var principal = MakePrincipal();

        var result = principal.GetEmail();

        Assert.Empty(result);
    }

    [Fact]
    public void GetUsernamePrefersPreferredUsernameClaim()
    {
        var principal = MakePrincipal(
            ("preferred_username", "n.baranov"),
            ("email", "n.baranov@urfu.me"));

        var result = principal.GetUsername();

        Assert.Equal("n.baranov", result);
    }

    [Fact]
    public void GetUsernameFallsBackToEmailClaim()
    {
        var principal = MakePrincipal(("email", "n.baranov@urfu.me"));

        var result = principal.GetUsername();

        Assert.Equal("n.baranov@urfu.me", result);
    }

    [Fact]
    public void GetUsernameReturnsEmptyWhenUsernameClaimsAreMissing()
    {
        var principal = MakePrincipal();

        var result = principal.GetUsername();

        Assert.Empty(result);
    }
}
