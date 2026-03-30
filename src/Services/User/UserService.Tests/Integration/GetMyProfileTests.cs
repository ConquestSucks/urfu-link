using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using UserService.Api.Application.Contracts.Responses;
using UserService.Tests.Infrastructure;
using Xunit;

namespace UserService.Tests.Integration;

public class GetMyProfileTests : IClassFixture<UserServiceFactory>
{
    private readonly UserServiceFactory _factory;

    public GetMyProfileTests(UserServiceFactory factory)
    {
        _factory = factory;
    }

    private static ClaimsPrincipal MakeUser(
        Guid userId,
        string sessionId = "sid-1",
        string name = "Никита Баранов",
        string email = "n.baranov@urfu.me",
        string username = "n.baranov")
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("sid", sessionId),
                new Claim("name", name),
                new Claim("email", email),
                new Claim("preferred_username", username),
            ],
            authenticationType: TestAuthHandler.SchemeName);
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task GetMe_AuthenticatedUser_ReturnsIdentityFromJwt()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = MakeUser(userId);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        body.Should().NotBeNull();
        body!.Identity.Name.Should().Be("Никита Баранов");
        body.Identity.Email.Should().Be("n.baranov@urfu.me");
        body.Identity.Username.Should().Be("n.baranov");
    }

    [Fact]
    public async Task GetMe_NewUser_CreatesDefaultProfile()
    {
        var userId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = MakeUser(userId);
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(userId);
        body.Account.AvatarUrl.Should().BeNull();
        body.Account.AboutMe.Should().BeNull();
        body.Privacy.ShowOnlineStatus.Should().BeTrue();
        body.Privacy.ShowLastVisitTime.Should().BeTrue();
    }

    [Fact]
    public async Task GetMe_Unauthenticated_Returns401()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
