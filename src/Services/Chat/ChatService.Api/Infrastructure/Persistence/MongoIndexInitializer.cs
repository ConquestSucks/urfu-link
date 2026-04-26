using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence;

/// <summary>
/// Creates the chat collection indexes at process startup. Safe to run repeatedly because the
/// driver no-ops on existing indexes with the same definition.
/// </summary>
internal sealed class MongoIndexInitializer(
    ChatMongoContext context,
    ILogger<MongoIndexInitializer>? logger = null) : IHostedService
{
    private readonly ILogger _logger = logger ?? NullLogger<MongoIndexInitializer>.Instance;

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

        await EnsureMessagesTextIndexAsync(messages, cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// Creates the full-text index on <c>messages.body</c> if it does not already exist.
    /// Tries Russian Snowball stemming first (works on stock MongoDB Community 8.0); on
    /// <see cref="MongoCommandException"/> indicating the language is unavailable, falls back
    /// to <c>"none"</c> (literal tokenization, no stemming) and logs a warning. The existence
    /// check up front keeps subsequent restarts idempotent — if a previous run fell back to
    /// <c>"none"</c>, we leave that index in place rather than failing on
    /// <c>IndexOptionsConflict</c> when retrying with Russian.
    /// </summary>
    private async Task EnsureMessagesTextIndexAsync(
        IMongoCollection<MessageDocument> messages,
        CancellationToken cancellationToken)
    {
        const string IndexName = "ix_messages_body_text";

        if (await IndexExistsAsync(messages, IndexName, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await messages.Indexes.CreateOneAsync(
                new CreateIndexModel<MessageDocument>(
                    Builders<MessageDocument>.IndexKeys.Text(m => m.Body),
                    new CreateIndexOptions
                    {
                        Name = IndexName,
                        Background = true,
                        DefaultLanguage = "russian",
                    }),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoCommandException ex) when (IsUnsupportedLanguage(ex))
        {
            _logger.LogWarning(
                ex,
                "MongoDB does not support Russian text-search language in this build; falling back to default_language=\"none\".");

            await messages.Indexes.CreateOneAsync(
                new CreateIndexModel<MessageDocument>(
                    Builders<MessageDocument>.IndexKeys.Text(m => m.Body),
                    new CreateIndexOptions
                    {
                        Name = IndexName,
                        Background = true,
                        DefaultLanguage = "none",
                    }),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> IndexExistsAsync(
        IMongoCollection<MessageDocument> messages,
        string indexName,
        CancellationToken cancellationToken)
    {
        using var cursor = await messages.Indexes
            .ListAsync(cancellationToken)
            .ConfigureAwait(false);
        var documents = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        return documents.Any(d => d.TryGetValue("name", out var value) && value.IsString && value.AsString == indexName);
    }

    private static bool IsUnsupportedLanguage(MongoCommandException ex)
        => ex.Code == 17262
            || ex.Message.Contains("language not supported", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("language override", StringComparison.OrdinalIgnoreCase);
}
