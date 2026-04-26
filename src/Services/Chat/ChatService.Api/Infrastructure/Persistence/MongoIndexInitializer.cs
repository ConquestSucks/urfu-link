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
                new CreateIndexModel<MessageDocument>(
                    Builders<MessageDocument>.IndexKeys.Ascending(m => m.Mentions),
                    new CreateIndexOptions
                    {
                        Name = "ix_messages_mentions",
                        Background = true,
                        Sparse = true,
                    }),
                // Thread-reply chronological lookups: GetThreadMessages walks replies for a given
                // root in createdAt order, so the keys are (threadRootId, createdAtUtc) ascending.
                // Sparse since main-flow messages have no threadRootId.
                new CreateIndexModel<MessageDocument>(
                    Builders<MessageDocument>.IndexKeys
                        .Ascending(m => m.ThreadRootId)
                        .Ascending(m => m.CreatedAtUtc),
                    new CreateIndexOptions
                    {
                        Name = "ix_messages_threadRoot_createdAt",
                        Background = true,
                        Sparse = true,
                    }),
            },
            cancellationToken).ConfigureAwait(false);

        var threadSubscriptions = context.ThreadSubscriptions;
        await threadSubscriptions.Indexes.CreateManyAsync(
            new[]
            {
                // Uniqueness on (rootMessageId, userId) prevents duplicate subscriptions and lets
                // UpsertAsync rely on it as an atomicity backstop.
                new CreateIndexModel<ThreadSubscriptionDocument>(
                    Builders<ThreadSubscriptionDocument>.IndexKeys
                        .Ascending(d => d.RootMessageId)
                        .Ascending(d => d.UserId),
                    new CreateIndexOptions
                    {
                        Name = "ux_thread_subscriptions_root_user",
                        Background = true,
                        Unique = true,
                    }),
                // Active-threads keyset pagination: filter by userId, sort by lastActivityAtUtc desc
                // then rootMessageId desc as a deterministic tie-breaker.
                new CreateIndexModel<ThreadSubscriptionDocument>(
                    Builders<ThreadSubscriptionDocument>.IndexKeys
                        .Ascending(d => d.UserId)
                        .Descending(d => d.LastActivityAtUtc)
                        .Descending(d => d.RootMessageId),
                    new CreateIndexOptions
                    {
                        Name = "ix_thread_subscriptions_user_lastActivity_desc",
                        Background = true,
                    }),
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
