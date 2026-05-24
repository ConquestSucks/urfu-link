using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Interfaces;

namespace PresenceService.IntegrationTests.Redis;

[Collection(IntegrationCollection.Name)]
public class RedisTypingStoreTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public RedisTypingStoreTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private ITypingStore Resolve() => _factory.Services.GetRequiredService<ITypingStore>();

    [Fact]
    public async Task StartTyping_New_ReturnsTrue()
    {
        var sut = Resolve();
        var conv = Guid.NewGuid();
        var user = Guid.NewGuid();

        var added = await sut.StartTypingAsync(conv, user, CancellationToken.None);

        added.Should().BeTrue();
        (await sut.IsTypingAsync(conv, user, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task StartTyping_Existing_ReturnsFalse()
    {
        var sut = Resolve();
        var conv = Guid.NewGuid();
        var user = Guid.NewGuid();
        await sut.StartTypingAsync(conv, user, CancellationToken.None);

        var added = await sut.StartTypingAsync(conv, user, CancellationToken.None);

        added.Should().BeFalse();
    }

    [Fact]
    public async Task StopTyping_Present_ReturnsTrue()
    {
        var sut = Resolve();
        var conv = Guid.NewGuid();
        var user = Guid.NewGuid();
        await sut.StartTypingAsync(conv, user, CancellationToken.None);

        var removed = await sut.StopTypingAsync(conv, user, CancellationToken.None);

        removed.Should().BeTrue();
        (await sut.IsTypingAsync(conv, user, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task StopTyping_Absent_ReturnsFalse()
    {
        var sut = Resolve();
        var conv = Guid.NewGuid();
        var user = Guid.NewGuid();

        var removed = await sut.StopTypingAsync(conv, user, CancellationToken.None);

        removed.Should().BeFalse();
    }

    [Fact]
    public async Task IsTyping_AfterTtl_ReturnsFalse()
    {
        // PresenceOptions.TypingTtl defaults to 5 seconds.
        var sut = Resolve();
        var conv = Guid.NewGuid();
        var user = Guid.NewGuid();
        await sut.StartTypingAsync(conv, user, CancellationToken.None);

        await Task.Delay(TimeSpan.FromSeconds(5.5));

        (await sut.IsTypingAsync(conv, user, CancellationToken.None)).Should().BeFalse();
    }
}
