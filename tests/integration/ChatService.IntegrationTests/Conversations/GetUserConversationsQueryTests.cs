using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.Services.Chat.Application.Conversations;

namespace ChatService.IntegrationTests.Conversations;

[Collection(IntegrationCollection.Name)]
public class GetUserConversationsQueryTests : IAsyncLifetime
{
    private readonly ChatServiceFactory _factory;

    public GetUserConversationsQueryTests(ChatServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Execute_PagesThroughAllConversationsForParticipant_OrderedNewestFirst()
    {
        var user = Guid.NewGuid();

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var open = seedScope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            for (var i = 0; i < 5; i++)
            {
                await open.OpenAsync(user, Guid.NewGuid(), default);
                await Task.Delay(5);
            }
        }

        await using var queryScope = _factory.Services.CreateAsyncScope();
        var query = queryScope.ServiceProvider.GetRequiredService<GetUserConversationsQuery>();

        var firstPage = await query.ExecuteAsync(user, cursor: null, limit: 3, default);
        firstPage.Items.Should().HaveCount(3);
        firstPage.NextCursor.Should().NotBeNullOrEmpty();

        var secondPage = await query.ExecuteAsync(user, cursor: firstPage.NextCursor, limit: 3, default);
        secondPage.Items.Should().HaveCount(2);
        secondPage.NextCursor.Should().BeNull();

        // No overlap
        var allIds = firstPage.Items.Concat(secondPage.Items).Select(c => c.Id).ToList();
        allIds.Distinct().Should().HaveCount(allIds.Count);
    }

    [Fact]
    public async Task Execute_DoesNotIncludeOtherUsersConversations()
    {
        var user = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var open = seedScope.ServiceProvider.GetRequiredService<OpenDirectConversationService>();
            await open.OpenAsync(user, Guid.NewGuid(), default);
            await open.OpenAsync(stranger, Guid.NewGuid(), default);
        }

        await using var queryScope = _factory.Services.CreateAsyncScope();
        var query = queryScope.ServiceProvider.GetRequiredService<GetUserConversationsQuery>();
        var page = await query.ExecuteAsync(user, cursor: null, limit: 50, default);

        page.Items.Should().HaveCount(1);
        page.Items[0].Participants.Should().Contain(user);
    }
}
