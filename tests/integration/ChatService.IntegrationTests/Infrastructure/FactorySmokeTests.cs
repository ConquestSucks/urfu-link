using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public class FactorySmokeTests
{
    private readonly ChatServiceFactory _factory;

    public FactorySmokeTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Factory_ResolvesChatMongoContext()
    {
        var ctx = _factory.Services.GetRequiredService<ChatMongoContext>();
        ctx.Should().NotBeNull();
    }

    [Fact]
    public void Factory_ResolvesRepositories()
    {
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IConversationRepository>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IMessageRepository>().Should().NotBeNull();
    }
}
