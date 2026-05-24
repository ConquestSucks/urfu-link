using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Conversations;
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
    public async Task OpenAsync_ReturnsDraftWithoutPersistingOrPublishing()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();

        var conversation = await service.OpenAsync(userA, userB, default);

        conversation.Participants.Should().BeEquivalentTo(new[] { userA, userB });
        conversation.LastMessagePreview.Should().BeNull();

        var loaded = await scope.ServiceProvider.GetRequiredService<IConversationRepository>()
            .GetByIdAsync(conversation.Id, default);
        loaded.Should().BeNull();
        _factory.OutboxWriter.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenAsync_RepeatedCalls_ReturnSameDraftId_WithoutPublishing()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();

        var first = await service.OpenAsync(userA, userB, default);
        var second = await service.OpenAsync(userB, userA, default);

        second.Id.Should().Be(first.Id);
        _factory.OutboxWriter.Published.Should().BeEmpty();
    }
}
