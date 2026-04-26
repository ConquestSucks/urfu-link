using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using Urfu.Link.BuildingBlocks.Idempotency;
using Xunit;

namespace ChatService.IntegrationTests.Infrastructure;

public sealed class RateLimiterTests : IClassFixture<ChatServiceFactory>, IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public RateLimiterTests(ChatServiceFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TryAcquire_AllowsUpToMaxRequests_ThenDenies()
    {
        var limiter = CreateLimiter("test-allows", TimeSpan.FromMinutes(1), maxRequests: 5);
        var key = $"user:{Guid.NewGuid():N}";

        for (var i = 0; i < 5; i++)
        {
            var decision = await limiter.TryAcquireAsync(key);
            decision.Allowed.Should().BeTrue($"request {i + 1} should be within the window");
        }

        var sixth = await limiter.TryAcquireAsync(key);
        sixth.Allowed.Should().BeFalse();
        sixth.RetryAfter.Should().NotBeNull();
        sixth.RetryAfter!.Value.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task TryAcquire_DifferentKeys_AreIsolated()
    {
        var limiter = CreateLimiter("test-isolation", TimeSpan.FromMinutes(1), maxRequests: 1);

        var first = await limiter.TryAcquireAsync($"a-{Guid.NewGuid():N}");
        var second = await limiter.TryAcquireAsync($"b-{Guid.NewGuid():N}");

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquire_AfterWindowElapses_AllowsAgain()
    {
        var limiter = CreateLimiter("test-reset", TimeSpan.FromMilliseconds(500), maxRequests: 1);
        var key = $"user:{Guid.NewGuid():N}";

        var first = await limiter.TryAcquireAsync(key);
        var blocked = await limiter.TryAcquireAsync(key);

        first.Allowed.Should().BeTrue();
        blocked.Allowed.Should().BeFalse();

        await Task.Delay(TimeSpan.FromMilliseconds(700));

        var afterWindow = await limiter.TryAcquireAsync(key);
        afterWindow.Allowed.Should().BeTrue();
    }

    private RedisFixedWindowRateLimiter CreateLimiter(string name, TimeSpan window, int maxRequests)
    {
        var multiplexer = _factory.Services.GetRequiredService<IConnectionMultiplexer>();
        return new RedisFixedWindowRateLimiter(multiplexer, new RateLimiterOptions
        {
            Name = name,
            Window = window,
            MaxRequests = maxRequests,
        });
    }
}
