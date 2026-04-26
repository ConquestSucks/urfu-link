using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;

namespace ChatService.IntegrationTests.Persistence;

public class MongoIndexInitializerTests : IClassFixture<MongoFixture>
{
    private readonly MongoFixture _mongo;

    public MongoIndexInitializerTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task StartAsync_CreatesAllExpectedIndexes()
    {
        using var context = _mongo.CreateContext();
        var initializer = new MongoIndexInitializer(context);

        await initializer.StartAsync(CancellationToken.None);

        var convIndexes = await context.Database
            .GetCollection<dynamic>(ChatMongoContext.ConversationsCollectionName)
            .Indexes.ListAsync();
        var convNames = (await convIndexes.ToListAsync()).Select(d => (string)d["name"]).ToList();
        convNames.Should().Contain(new[] { "ix_conversations_participants", "ix_conversations_lastMessageAtUtc_desc" });

        var msgIndexes = await context.Database
            .GetCollection<dynamic>(ChatMongoContext.MessagesCollectionName)
            .Indexes.ListAsync();
        var msgNames = (await msgIndexes.ToListAsync()).Select(d => (string)d["name"]).ToList();
        msgNames.Should().Contain(new[]
        {
            "ix_messages_conversation_createdAt_desc",
            "ix_messages_sender_createdAt_desc",
            "ux_messages_sender_clientMessageId",
            "ix_messages_mentions",
            "ix_messages_body_text",
        });
    }

    [Fact]
    public async Task StartAsync_IsIdempotent()
    {
        using var context = _mongo.CreateContext();
        var initializer = new MongoIndexInitializer(context);

        await initializer.StartAsync(CancellationToken.None);
        await initializer.StartAsync(CancellationToken.None);

        var convIndexes = await context.Database
            .GetCollection<dynamic>(ChatMongoContext.ConversationsCollectionName)
            .Indexes.ListAsync();
        var convCount = (await convIndexes.ToListAsync()).Count;
        // _id + 2 of ours = 3
        convCount.Should().Be(3);
    }
}
