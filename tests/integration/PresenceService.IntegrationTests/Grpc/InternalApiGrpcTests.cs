using FluentAssertions;
using global::Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using PresenceService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Presence.Domain.Enums;
using Urfu.Link.Services.Presence.Domain.Interfaces;
using Urfu.Link.Services.Presence.Domain.ValueObjects;
using Urfu.Link.Services.Presence.Grpc;
using DomainEnums = Urfu.Link.Services.Presence.Domain.Enums;

namespace PresenceService.IntegrationTests.Grpc;

[Collection(IntegrationCollection.Name)]
public sealed class InternalApiGrpcTests : IAsyncLifetime
{
    private readonly PresenceServiceFactory _factory;
    private HttpClient _httpClient = null!;
    private GrpcChannel _channel = null!;

    public InternalApiGrpcTests(PresenceServiceFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDataAsync();
        _httpClient = _factory.CreateClient();
        _channel = GrpcChannel.ForAddress(_httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = _httpClient });
    }

    public Task DisposeAsync()
    {
        _channel.Dispose();
        _httpClient.Dispose();
        return Task.CompletedTask;
    }

    private InternalApi.InternalApiClient AuthorizedClient(Guid userId)
    {
        TestAuthHandler.CurrentPrincipal = TestUserBuilder.MakeUser(userId);
        return new InternalApi.InternalApiClient(_channel);
    }

    [Fact]
    public async Task Ping_ReturnsPongPlusEcho()
    {
        var client = AuthorizedClient(Guid.NewGuid());

        var reply = await client.PingAsync(new PingRequest { Message = "hi" });

        reply.Service.Should().Be("presence-service");
        reply.Message.Should().Be("pong:hi");
    }

    [Fact]
    public async Task GetPresence_ReturnsRealStatusIgnoringPrivacy()
    {
        var caller = Guid.NewGuid();
        var target = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
            await sessions.AddSessionAsync(new PresenceSession(
                target, "d1", DomainEnums.Platform.Web, DomainEnums.PresenceStatus.Online,
                null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CancellationToken.None);
        }
        // Privacy says hide everything — gRPC should ignore that.
        await TestKafkaTrigger.TriggerPrivacyChangedAsync(_factory, target, false, false);

        var client = AuthorizedClient(caller);
        var reply = await client.GetPresenceAsync(new GetPresenceRequest { UserId = target.ToString() });

        reply.Status.Should().Be(global::Urfu.Link.Services.Presence.Grpc.PresenceStatus.Online);
        reply.Platforms.Should().Contain(global::Urfu.Link.Services.Presence.Grpc.Platform.Web);
    }

    [Fact]
    public async Task IsOnline_TrueWhenSessionExists()
    {
        var caller = Guid.NewGuid();
        var target = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
            await sessions.AddSessionAsync(new PresenceSession(
                target, "d1", DomainEnums.Platform.Mobile, DomainEnums.PresenceStatus.Online,
                null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CancellationToken.None);
        }

        var client = AuthorizedClient(caller);
        var reply = await client.IsOnlineAsync(new IsOnlineRequest { UserId = target.ToString() });

        reply.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task IsOnline_FalseWhenNoSession()
    {
        var client = AuthorizedClient(Guid.NewGuid());
        var reply = await client.IsOnlineAsync(new IsOnlineRequest { UserId = Guid.NewGuid().ToString() });
        reply.IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task IsTyping_TrueAfterStart()
    {
        var caller = Guid.NewGuid();
        var conv = Guid.NewGuid();
        var target = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var typing = scope.ServiceProvider.GetRequiredService<ITypingStore>();
            await typing.StartTypingAsync(conv, target, CancellationToken.None);
        }

        var client = AuthorizedClient(caller);
        var reply = await client.IsTypingAsync(new IsTypingRequest
        {
            ConversationId = conv.ToString(),
            UserId = target.ToString(),
        });

        reply.IsTyping.Should().BeTrue();
    }

    [Fact]
    public async Task GetPresenceBatch_ReturnsForAllRequested()
    {
        var caller = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<IPresenceSessionStore>();
            await sessions.AddSessionAsync(new PresenceSession(
                ids[0], "d1", DomainEnums.Platform.Web, DomainEnums.PresenceStatus.Online,
                null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CancellationToken.None);
        }

        var client = AuthorizedClient(caller);
        var req = new GetPresenceBatchRequest();
        foreach (var id in ids) req.UserIds.Add(id.ToString());
        var reply = await client.GetPresenceBatchAsync(req);

        reply.Items.Should().HaveCount(3);
        reply.Items.Should().ContainSingle(i => i.UserId == ids[0].ToString()
            && i.Status == global::Urfu.Link.Services.Presence.Grpc.PresenceStatus.Online);
    }

    [Fact]
    public async Task GetPresence_InvalidUuid_ReturnsInvalidArgument()
    {
        var client = AuthorizedClient(Guid.NewGuid());
        var act = () => client.GetPresenceAsync(new GetPresenceRequest { UserId = "not-a-guid" }).ResponseAsync;
        await act.Should().ThrowAsync<global::Grpc.Core.RpcException>()
            .Where(ex => ex.StatusCode == global::Grpc.Core.StatusCode.InvalidArgument);
    }
}
