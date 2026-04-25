using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Application.Contracts.Responses;
using Urfu.Link.Services.Presence.Domain.Aggregates;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace PresenceService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class GetUserPresenceTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public GetUserPresenceTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Get_NoSessionsNoLastSeen_ReturnsOfflineWithNullLastSeen()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);

        var response = await _factory.CreateClient().GetAsync($"/api/v1/presence/users/{targetId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PresenceInfoResponse>();
        body.Should().NotBeNull();
        body!.UserId.Should().Be(targetId);
        body.Status.Should().Be(PresenceStatus.Offline);
        body.Platforms.Should().BeEmpty();
        body.LastSeenAt.Should().BeNull();
    }

    [Fact]
    public async Task Get_WithOnlineSession_ReturnsOnline()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
        var ts = DateTimeOffset.UtcNow;
        await sessions.AddSessionAsync(new PresenceSession(
            targetId, "d1", Platform.Web, PresenceStatus.Online,
            CustomActivity: null, ConnectedAt: ts, LastHeartbeatAt: ts), CancellationToken.None);

        var response = await _factory.CreateClient().GetAsync($"/api/v1/presence/users/{targetId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PresenceInfoResponse>();
        body!.Status.Should().Be(PresenceStatus.Online);
        body.Platforms.Should().Equal(Platform.Web);
    }

    [Fact]
    public async Task Get_WithLastSeen_PassesLastSeenThrough()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);
        var ts = DateTimeOffset.UtcNow.AddMinutes(-5);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<Urfu.Link.Services.Presence.Infrastructure.Persistence.PresenceDbContext>();
            ctx.LastSeens.Add(LastSeen.Create(targetId, Platform.Mobile, ts));
            await ctx.SaveChangesAsync();
        }

        var response = await _factory.CreateClient().GetAsync($"/api/v1/presence/users/{targetId}");

        var body = await response.Content.ReadFromJsonAsync<PresenceInfoResponse>();
        body!.LastSeenAt.Should().BeCloseTo(ts, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Get_PrivacyHidesOnlineStatus_ReturnsOffline()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
            await sessions.AddSessionAsync(new PresenceSession(
                targetId, "d1", Platform.Web, PresenceStatus.Online,
                null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CancellationToken.None);
        }
        await TestKafkaTrigger.TriggerPrivacyChangedAsync(_factory, targetId, showOnlineStatus: false, showLastVisitTime: true);

        var response = await _factory.CreateClient().GetAsync($"/api/v1/presence/users/{targetId}");

        var body = await response.Content.ReadFromJsonAsync<PresenceInfoResponse>();
        body!.Status.Should().Be(PresenceStatus.Offline);
        body.Platforms.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_PrivacyHidesLastVisit_ReturnsNullLastSeen()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(requesterId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<Urfu.Link.Services.Presence.Infrastructure.Persistence.PresenceDbContext>();
            ctx.LastSeens.Add(LastSeen.Create(targetId, Platform.Mobile, DateTimeOffset.UtcNow));
            await ctx.SaveChangesAsync();
        }
        await TestKafkaTrigger.TriggerPrivacyChangedAsync(_factory, targetId, showOnlineStatus: true, showLastVisitTime: false);

        var response = await _factory.CreateClient().GetAsync($"/api/v1/presence/users/{targetId}");

        var body = await response.Content.ReadFromJsonAsync<PresenceInfoResponse>();
        body!.LastSeenAt.Should().BeNull();
    }

    [Fact]
    public async Task Get_Unauthorized_Returns401()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var response = await _factory.CreateClient().GetAsync($"/api/v1/presence/users/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
