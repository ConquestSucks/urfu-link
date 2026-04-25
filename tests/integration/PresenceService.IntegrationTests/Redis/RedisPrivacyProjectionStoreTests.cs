using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;

namespace PresenceService.IntegrationTests.Redis;

[Collection(IntegrationCollection.Name)]
public class RedisPrivacyProjectionStoreTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public RedisPrivacyProjectionStoreTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private IPrivacyProjectionStore Resolve() =>
        _factory.Services.GetRequiredService<IPrivacyProjectionStore>();

    [Fact]
    public async Task Get_NotSet_ReturnsDefault()
    {
        var sut = Resolve();

        var result = await sut.GetAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().Be(PrivacySettings.Default);
    }

    [Fact]
    public async Task SetThenGet_RoundTrips()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        var settings = new PrivacySettings(ShowOnlineStatus: false, ShowLastVisitTime: true);

        await sut.SetAsync(userId, settings, CancellationToken.None);
        var loaded = await sut.GetAsync(userId, CancellationToken.None);

        loaded.Should().Be(settings);
    }

    [Fact]
    public async Task Set_OverridesPrevious()
    {
        var sut = Resolve();
        var userId = Guid.NewGuid();
        await sut.SetAsync(userId, new PrivacySettings(false, false), CancellationToken.None);

        await sut.SetAsync(userId, new PrivacySettings(true, true), CancellationToken.None);

        var loaded = await sut.GetAsync(userId, CancellationToken.None);
        loaded.Should().Be(new PrivacySettings(true, true));
    }
}
