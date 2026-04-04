using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Testcontainers.Redis;
using Urfu.Link.BuildingBlocks.SessionRevocation;

namespace SessionRevocation.Tests;

public sealed class RedisSessionRevocationStoreTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().Build();
    private ConnectionMultiplexer? _multiplexer;
    private RedisSessionRevocationStore _store = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        _store = CreateStore(TimeSpan.FromSeconds(300));
    }

    public async Task DisposeAsync()
    {
        _multiplexer?.Dispose();
        await _redis.DisposeAsync();
    }

    private RedisSessionRevocationStore CreateStore(TimeSpan ttl)
    {
        var options = Options.Create(new SessionRevocationOptions
        {
            KeyPrefix = "urfu:session",
            Ttl = ttl,
        });
        return new RedisSessionRevocationStore(_multiplexer!, options);
    }

    [Fact]
    public async Task ShouldReturnFalseWhenNoRevocation()
    {
        var result = await _store.IsRevokedAsync("user-1", "session-abc");

        Assert.False(result);
    }

    [Fact]
    public async Task ShouldSetKeysInRedisOnRevoke()
    {
        await _store.RevokeAsync("user-1", "caller-session");

        var db = _multiplexer!.GetDatabase();
        Assert.True(await db.KeyExistsAsync("urfu:session:revoked:user-1"));
        Assert.True(await db.SetContainsAsync("urfu:session:allowed:user-1", "caller-session"));
    }

    [Fact]
    public async Task ShouldReturnTrueForNonAllowedSession()
    {
        await _store.RevokeAsync("user-1", "caller-session");

        var result = await _store.IsRevokedAsync("user-1", "other-session");

        Assert.True(result);
    }

    [Fact]
    public async Task ShouldReturnFalseForAllowedSession()
    {
        await _store.RevokeAsync("user-1", "caller-session");

        var result = await _store.IsRevokedAsync("user-1", "caller-session");

        Assert.False(result);
    }

    [Fact]
    public async Task ShouldAccumulateAllowedSessions()
    {
        await _store.RevokeAsync("user-1", "session-a");
        await _store.RevokeAsync("user-1", "session-b");

        Assert.False(await _store.IsRevokedAsync("user-1", "session-a"));
        Assert.False(await _store.IsRevokedAsync("user-1", "session-b"));
        Assert.True(await _store.IsRevokedAsync("user-1", "session-c"));
    }

    [Fact]
    public async Task ShouldExpireKeysAfterTtl()
    {
        var shortStore = CreateStore(TimeSpan.FromSeconds(1));

        await shortStore.RevokeAsync("user-ttl", "caller");
        Assert.True(await shortStore.IsRevokedAsync("user-ttl", "other"));

        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.False(await shortStore.IsRevokedAsync("user-ttl", "other"));
    }

    [Fact]
    public async Task ShouldIsolateDifferentUsers()
    {
        await _store.RevokeAsync("user-a", "session-1");

        Assert.True(await _store.IsRevokedAsync("user-a", "session-2"));
        Assert.False(await _store.IsRevokedAsync("user-b", "session-2"));
    }

    [Fact]
    public async Task RevokeSingleShouldRevokeTargetSession()
    {
        await _store.RevokeSingleAsync("user-1", "session-bad");

        Assert.True(await _store.IsRevokedAsync("user-1", "session-bad"));
    }

    [Fact]
    public async Task RevokeSingleShouldNotAffectOtherSessions()
    {
        await _store.RevokeSingleAsync("user-1", "session-bad");

        Assert.False(await _store.IsRevokedAsync("user-1", "session-good"));
        Assert.False(await _store.IsRevokedAsync("user-1", "session-other"));
    }

    [Fact]
    public async Task RevokeSingleShouldNotAffectOtherUsers()
    {
        await _store.RevokeSingleAsync("user-a", "session-bad");

        Assert.False(await _store.IsRevokedAsync("user-b", "session-bad"));
    }

    [Fact]
    public async Task RevokeSingleShouldExpireAfterTtl()
    {
        var shortStore = CreateStore(TimeSpan.FromSeconds(1));

        await shortStore.RevokeSingleAsync("user-ttl2", "session-bad");
        Assert.True(await shortStore.IsRevokedAsync("user-ttl2", "session-bad"));

        await Task.Delay(TimeSpan.FromSeconds(2));

        Assert.False(await shortStore.IsRevokedAsync("user-ttl2", "session-bad"));
    }

    [Fact]
    public async Task RevokeSingleShouldSetDeniedKeyInRedis()
    {
        await _store.RevokeSingleAsync("user-1", "session-bad");

        var db = _multiplexer!.GetDatabase();
        Assert.True(await db.SetContainsAsync("urfu:session:denied:user-1", "session-bad"));
    }

    [Fact]
    public async Task RevokeSingleShouldNotSetGlobalRevokedFlag()
    {
        await _store.RevokeSingleAsync("user-1", "session-bad");

        var db = _multiplexer!.GetDatabase();
        Assert.False(await db.KeyExistsAsync("urfu:session:revoked:user-1"));
    }

    [Fact]
    public async Task RevokeSingleAndBulkRevokeShouldCompose()
    {
        // session-bad denied individually, session-caller allowed via bulk revoke
        await _store.RevokeSingleAsync("user-1", "session-bad");
        await _store.RevokeAsync("user-1", "session-caller");

        Assert.True(await _store.IsRevokedAsync("user-1", "session-bad"));
        Assert.True(await _store.IsRevokedAsync("user-1", "session-other"));
        Assert.False(await _store.IsRevokedAsync("user-1", "session-caller"));
    }
}
