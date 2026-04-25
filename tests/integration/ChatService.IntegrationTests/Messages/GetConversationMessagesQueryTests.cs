using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application;
using Urfu.Link.Services.Chat.Application.Conversations;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;

namespace ChatService.IntegrationTests.Messages;

[Collection(IntegrationCollection.Name)]
public class GetConversationMessagesQueryTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public GetConversationMessagesQueryTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Execute_OlderDirection_PagesFromNewestToOldest()
    {
        var (sender, recipient, convId) = await SeedConversationWithMessagesAsync(messageCount: 5);

        await using var scope = _factory.Services.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<GetConversationMessagesQuery>();

        var page = await query.ExecuteAsync(convId, sender, cursor: null, limit: 3, CursorDirection.Older, default);
        page.Items.Should().HaveCount(3);
        page.Items.Select(m => m.Body).Should().Equal("m4", "m3", "m2");
        page.NextCursor.Should().NotBeNullOrEmpty();

        var next = await query.ExecuteAsync(convId, recipient, cursor: page.NextCursor, limit: 3, CursorDirection.Older, default);
        next.Items.Select(m => m.Body).Should().Equal("m1", "m0");
        next.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Execute_NonParticipant_ThrowsAccessDenied()
    {
        var (_, _, convId) = await SeedConversationWithMessagesAsync(messageCount: 1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var query = scope.ServiceProvider.GetRequiredService<GetConversationMessagesQuery>();

        var act = () => query.ExecuteAsync(convId, Guid.NewGuid(), cursor: null, limit: 10, CursorDirection.Older, default);
        await act.Should().ThrowAsync<ChatAccessDeniedException>();
    }

    private async Task<(Guid sender, Guid recipient, string convId)> SeedConversationWithMessagesAsync(int messageCount)
    {
        var sender = Guid.NewGuid();
        var recipient = Guid.NewGuid();
        string convId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var open = scope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            var conv = await open.OpenAsync(sender, recipient, default);
            convId = conv.Id;
        }

        for (var i = 0; i < messageCount; i++)
        {
            await using var sendScope = _factory.Services.CreateAsyncScope();
            var send = sendScope.ServiceProvider.GetRequiredService<SendMessageService>();
            await send.SendAsync(
                new SendMessageRequest(convId, sender, $"m{i}", Array.Empty<Guid>(), $"c-{Guid.NewGuid():N}"),
                default);
            await Task.Delay(5);
        }

        return (sender, recipient, convId);
    }
}
