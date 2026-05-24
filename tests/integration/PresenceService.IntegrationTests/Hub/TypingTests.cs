using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;

namespace PresenceService.IntegrationTests.Hub;

[Collection(IntegrationCollection.Name)]
public class TypingTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;

    public TypingTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task StartTyping_BroadcastsToSelfGroup()
    {
        var userId = Guid.NewGuid();
        var conv = Guid.NewGuid();
        var received = new TaskCompletionSource<(Guid ConvId, Guid UserId, bool IsTyping)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = await TestPresenceHubClient.ConnectAsync(_factory, userId, Platform.Web);
        connection.On<Guid, Guid, bool>("UserTyping",
            (cid, uid, isTyping) => received.TrySetResult((cid, uid, isTyping)));

        await connection.InvokeAsync("StartTyping", conv);

        var (cid, uid, t) = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cid.Should().Be(conv);
        uid.Should().Be(userId);
        t.Should().BeTrue();
    }

    [Fact]
    public async Task StopTyping_BroadcastsTypingFalse()
    {
        var userId = Guid.NewGuid();
        var conv = Guid.NewGuid();
        var stopReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = await TestPresenceHubClient.ConnectAsync(_factory, userId);
        await connection.InvokeAsync("StartTyping", conv);
        connection.On<Guid, Guid, bool>("UserTyping",
            (_, _, isTyping) =>
            {
                if (!isTyping) stopReceived.TrySetResult(true);
            });

        await connection.InvokeAsync("StopTyping", conv);

        (await stopReceived.Task.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue();
    }

    [Fact]
    public async Task IsTyping_AfterStart_ReturnsTrueViaTypingStore()
    {
        var userId = Guid.NewGuid();
        var conv = Guid.NewGuid();
        await using var connection = await TestPresenceHubClient.ConnectAsync(_factory, userId);

        await connection.InvokeAsync("StartTyping", conv);

        await using var scope = _factory.Services.CreateAsyncScope();
        var typing = scope.ServiceProvider.GetRequiredService<ITypingStore>();
        (await typing.IsTypingAsync(conv, userId, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Typing_AutoExpiresAfterTtl()
    {
        var userId = Guid.NewGuid();
        var conv = Guid.NewGuid();
        await using var connection = await TestPresenceHubClient.ConnectAsync(_factory, userId);

        await connection.InvokeAsync("StartTyping", conv);
        await Task.Delay(TimeSpan.FromSeconds(5.5));

        await using var scope = _factory.Services.CreateAsyncScope();
        var typing = scope.ServiceProvider.GetRequiredService<ITypingStore>();
        (await typing.IsTypingAsync(conv, userId, CancellationToken.None)).Should().BeFalse();
    }
}
