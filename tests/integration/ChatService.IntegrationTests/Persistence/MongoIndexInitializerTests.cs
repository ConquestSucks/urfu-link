using ChatService.IntegrationTests.Infrastructure;
using FluentAssertions;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Infrastructure.Persistence;
using Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

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
        convNames.Should().Contain(new[]
        {
            "ix_conversations_participants",
            "ix_conversations_lastMessageAtUtc_desc",
            "ux_conversations_discipline_scope",
        });

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
        // _id + 3 of ours = 4
        convCount.Should().Be(4);
    }

    [Fact]
    public async Task StartAsync_TextIndexAlreadyExistsWithDifferentLanguage_DoesNotThrow()
    {
        // Simulate a prior fallback run: a "none"-language text index already lives in the
        // collection. The default code path tries Russian first; without an existence check
        // Mongo would reject re-creation with IndexOptionsConflict (NOT an unsupported-language
        // error), so the catch wouldn't match and startup would crash on every restart.
        using var context = _mongo.CreateContext();
        await context.Messages.Indexes.CreateOneAsync(
            new CreateIndexModel<MessageDocument>(
                Builders<MessageDocument>.IndexKeys.Text(m => m.Body),
                new CreateIndexOptions
                {
                    Name = "ix_messages_body_text",
                    Background = true,
                    DefaultLanguage = "none",
                }));

        var initializer = new MongoIndexInitializer(context);
        var act = () => initializer.StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();

        var indexes = await context.Database
            .GetCollection<dynamic>(ChatMongoContext.MessagesCollectionName)
            .Indexes.ListAsync();
        var names = (await indexes.ToListAsync()).Select(d => (string)d["name"]).ToList();
        names.Should().Contain("ix_messages_body_text");
    }
}
