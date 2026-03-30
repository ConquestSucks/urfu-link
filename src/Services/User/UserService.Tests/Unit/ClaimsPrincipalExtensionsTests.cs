using System.Security.Claims;
using FluentAssertions;
using UserService.Api.Infrastructure.Auth;
using Xunit;

namespace UserService.Tests.Unit;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal MakePrincipal(params (string type, string value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.type, c.value)),
            authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void GetUserId_SubClaim_ReturnsGuid()
    {
        var id = Guid.NewGuid();
        var principal = MakePrincipal(("sub", id.ToString()));

        principal.GetUserId().Should().Be(id);
    }

    [Fact]
    public void GetUserId_MissingSubClaim_Throws()
    {
        var principal = MakePrincipal();

        var act = () => principal.GetUserId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetSessionId_SidClaim_ReturnsValue()
    {
        var principal = MakePrincipal(("sid", "session-abc"));

        principal.GetSessionId().Should().Be("session-abc");
    }

    [Fact]
    public void GetSessionId_MissingSidClaim_Throws()
    {
        var principal = MakePrincipal();

        var act = () => principal.GetSessionId();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetDisplayName_NameClaim_ReturnsName()
    {
        var principal = MakePrincipal(("name", "Никита Баранов"), ("preferred_username", "n.baranov"));

        principal.GetDisplayName().Should().Be("Никита Баранов");
    }

    [Fact]
    public void GetDisplayName_NoNameClaim_FallsBackToUsername()
    {
        var principal = MakePrincipal(("preferred_username", "n.baranov"));

        principal.GetDisplayName().Should().Be("n.baranov");
    }

    [Fact]
    public void GetDisplayName_NoClaims_ReturnsEmpty()
    {
        var principal = MakePrincipal();

        principal.GetDisplayName().Should().BeEmpty();
    }

    [Fact]
    public void GetEmail_EmailClaim_ReturnsEmail()
    {
        var principal = MakePrincipal(("email", "n.baranov@urfu.me"));

        principal.GetEmail().Should().Be("n.baranov@urfu.me");
    }

    [Fact]
    public void GetEmail_NoClaim_ReturnsEmpty()
    {
        var principal = MakePrincipal();

        principal.GetEmail().Should().BeEmpty();
    }

    [Fact]
    public void GetUsername_PreferredUsernameClaim_ReturnsValue()
    {
        var principal = MakePrincipal(("preferred_username", "n.baranov"));

        principal.GetUsername().Should().Be("n.baranov");
    }

    [Fact]
    public void GetUsername_NoClaim_ReturnsEmpty()
    {
        var principal = MakePrincipal();

        principal.GetUsername().Should().BeEmpty();
    }
}
