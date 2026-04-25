using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Domain.Events;
using Urfu.Link.Services.Chat.Domain.Interfaces;

namespace ChatService.IntegrationTests.Conversations;

[Collection(IntegrationCollection.Name)]
public class OpenDirectConversationServiceTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public OpenDirectConversationServiceTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task OpenAsync_PersistsConversationAndPublishesCreatedEvent()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();

        var conversation = await service.OpenAsync(userA, userB, default);

        conversation.Participants.Should().BeEquivalentTo(new[] { userA, userB });

        var loaded = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
            .GetByIdAsync(conversation.Id, default);
        loaded.Should().NotBeNull();

        var published = _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<ChatConversationCreatedEvent>()
            .Where(e => e.ConversationId == conversation.Id)
            .ToList();
        published.Should().ContainSingle();
    }

    [Fact]
    public async Task OpenAsync_RepeatedCalls_AreIdempotent_AndDoNotDoublePublish()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();

        var first = await service.OpenAsync(userA, userB, default);
        var second = await service.OpenAsync(userB, userA, default);

        second.Id.Should().Be(first.Id);

        var published = _factory.OutboxWriter.Published
            .OfType<ChatService.IntegrationTests.Infrastructure.PublishedEvent>()
            .Where(p => p.Payload is ChatConversationCreatedEvent ev && ev.ConversationId == first.Id)
            .ToList();
        published.Should().HaveCount(1);
    }
}
