using MongoDB.Driver;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
using Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence;

internal sealed class ConversationRepository(ChatMongoContext context) : IConversationRepository
{
    public async Task<Conversation?> GetByIdAsync(string conversationId, CancellationToken cancellationToken)
    {
        var doc = await context.Conversations
            .Find(c => c.Id == conversationId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return doc?.ToDomain();
    }

    public async Task<bool> TryCreateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        var doc = ConversationDocument.FromDomain(conversation);
        try
        {
            await context.Conversations.InsertOneAsync(doc, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task UpdateLastMessageAsync(
        string conversationId,
        MessagePreview preview,
        DateTimeOffset lastMessageAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var update = Builders<ConversationDocument>.Update
            .Set(c => c.LastMessagePreview, MessagePreviewDocument.FromDomain(preview))
            .Set(c => c.LastMessageAtUtc, lastMessageAtUtc.UtcDateTime);
        await context.Conversations
            .UpdateOneAsync(c => c.Id == conversationId, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Conversation>> ListByParticipantAsync(
        Guid userId,
        ConversationCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return Array.Empty<Conversation>();
        }

        var filterBuilder = Builders<ConversationDocument>.Filter;
        var filter = filterBuilder.AnyEq(c => c.Participants, userId);

        if (cursor is { } c)
        {
            var ts = c.LastMessageAtUtc.UtcDateTime;
            // Strict descending paging: take rows older than the cursor, breaking ties by Id desc.
            var cursorFilter = filterBuilder.Or(
                filterBuilder.Lt(d => d.LastMessageAtUtc, ts),
                filterBuilder.And(
                    filterBuilder.Eq(d => d.LastMessageAtUtc, ts),
                    filterBuilder.Lt(d => d.Id, c.ConversationId)));
            filter = filterBuilder.And(filter, cursorFilter);
        }

        var docs = await context.Conversations
            .Find(filter)
            .Sort(Builders<ConversationDocument>.Sort
                .Descending(c => c.LastMessageAtUtc)
                .Descending(c => c.Id))
            .Limit(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return docs.Select(d => d.ToDomain()).ToList();
    }
}
