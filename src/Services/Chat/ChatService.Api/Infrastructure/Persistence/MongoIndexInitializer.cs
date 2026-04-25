using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence;

/// <summary>
/// Creates the chat collection indexes at process startup. Safe to run repeatedly because the
/// driver no-ops on existing indexes with the same definition.
/// </summary>
internal sealed class MongoIndexInitializer(ChatMongoContext context) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var conversations = context.Conversations;
        await conversations.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<ConversationDocument>(
                    Builders<ConversationDocument>.IndexKeys.Ascending(c => c.Participants),
                    new CreateIndexOptions { Name = "ix_conversations_participants", Background = true }),
                new CreateIndexModel<ConversationDocument>(
                    Builders<ConversationDocument>.IndexKeys.Descending(c => c.LastMessageAtUtc),
                    new CreateIndexOptions { Name = "ix_conversations_lastMessageAtUtc_desc", Background = true }),
            },
            cancellationToken).ConfigureAwait(false);

        var messages = context.Messages;
        await messages.Indexes.CreateManyAsync(
            new[]
            {
                new CreateIndexModel<MessageDocument>(
                    Builders<MessageDocument>.IndexKeys
                        .Ascending(m => m.ConversationId)
                        .Descending(m => m.CreatedAtUtc),
                    new CreateIndexOptions { Name = "ix_messages_conversation_createdAt_desc", Background = true }),
                new CreateIndexModel<MessageDocument>(
                    Builders<MessageDocument>.IndexKeys
                        .Ascending(m => m.SenderId)
                        .Descending(m => m.CreatedAtUtc),
                    new CreateIndexOptions { Name = "ix_messages_sender_createdAt_desc", Background = true }),
                new CreateIndexModel<MessageDocument>(
                    Builders<MessageDocument>.IndexKeys
                        .Ascending(m => m.SenderId)
                        .Ascending(m => m.ClientMessageId),
                    new CreateIndexOptions
                    {
                        Name = "ux_messages_sender_clientMessageId",
                        Background = true,
                        Unique = true,
                        Sparse = true,
                    }),
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
