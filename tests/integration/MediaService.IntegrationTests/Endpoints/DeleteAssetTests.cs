using System.Net;
using System.Security.Claims;
using FluentAssertions;
using MediaService.IntegrationTests.Infrastructure;

namespace MediaService.IntegrationTests.Endpoints;

public class DeleteAssetTests : IClassFixture<MediaServiceFactory>
{
    private readonly MediaServiceFactory _factory;

    public DeleteAssetTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    private static ClaimsPrincipal MakeUser(Guid userId)
        => new(new ClaimsIdentity([new Claim("sub", userId.ToString())], TestAuthHandler.SchemeName));

    [Fact]
    public async Task DeleteAsset_MissingIdempotencyKey_Returns400()
    {
        TestAuthHandler.CurrentPrincipal = MakeUser(Guid.NewGuid());
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/v1/media/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
