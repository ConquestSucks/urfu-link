using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Application.Contracts.Requests;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;
using Urfu.Link.Services.Presence.Endpoints;

namespace PresenceService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class BatchPresenceTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public BatchPresenceTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Batch_ReturnsAllRequested()
    {
        var requesterId = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
            var ts = DateTimeOffset.UtcNow;
            await sessions.AddSessionAsync(new PresenceSession(
                ids[0], "d", Platform.Web, PresenceStatus.Online, null, ts, ts), CancellationToken.None);
        }

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/presence/users/batch",
            new BatchPresenceRequest { UserIds = ids });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BatchPresenceResponse>();
        body.Should().NotBeNull();
        body!.Items.Should().HaveCount(3);
        body.Items.Should().ContainSingle(i => i.UserId == ids[0] && i.Status == PresenceStatus.Online);
        body.Items.Where(i => i.UserId == ids[1] || i.UserId == ids[2])
            .Should().AllSatisfy(i => i.Status.Should().Be(PresenceStatus.Offline));
    }

    [Fact]
    public async Task Batch_OverLimit_Returns400()
    {
        var requesterId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);
        var tooMany = Enumerable.Range(0, BatchPresenceRequest.MaxUserIds + 1)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/presence/users/batch",
            new BatchPresenceRequest { UserIds = tooMany });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Batch_Empty_Returns400()
    {
        var requesterId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/presence/users/batch",
            new BatchPresenceRequest { UserIds = [] });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Batch_Unauthorized_Returns401()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/v1/presence/users/batch",
            new BatchPresenceRequest { UserIds = [Guid.NewGuid()] });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
