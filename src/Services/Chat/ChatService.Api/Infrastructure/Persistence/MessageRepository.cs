using MongoDB.Driver;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence;

internal sealed class MessageRepository(ChatMongoContext context) : IMessageRepository
{
    public async Task<Message?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var doc = await context.Messages
            .Find(m => m.Id == messageId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return doc?.ToDomain();
    }

    public async Task InsertAsync(Message message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var doc = MessageDocument.FromDomain(message);
        try
        {
            await context.Messages.InsertOneAsync(doc, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateClientMessageException(message.SenderId, message.ClientMessageId);
        }
    }

    public async Task<Message?> FindByClientMessageIdAsync(
        Guid senderId,
        string clientMessageId,
        CancellationToken cancellationToken)
    {
        var doc = await context.Messages
            .Find(m => m.SenderId == senderId && m.ClientMessageId == clientMessageId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return doc?.ToDomain();
    }

    public async Task<IReadOnlyList<Message>> ListByConversationAsync(
        string conversationId,
        MessageCursor? cursor,
        int limit,
        CursorDirection direction,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return Array.Empty<Message>();
        }

        var fb = Builders<MessageDocument>.Filter;
        var filter = fb.Eq(m => m.ConversationId, conversationId);

        if (cursor is { } c)
        {
            var ts = c.CreatedAtUtc.UtcDateTime;
            filter = direction == CursorDirection.Older
                ? fb.And(filter, fb.Or(
                    fb.Lt(m => m.CreatedAtUtc, ts),
                    fb.And(fb.Eq(m => m.CreatedAtUtc, ts), fb.Lt(m => m.Id, c.MessageId))))
                : fb.And(filter, fb.Or(
                    fb.Gt(m => m.CreatedAtUtc, ts),
                    fb.And(fb.Eq(m => m.CreatedAtUtc, ts), fb.Gt(m => m.Id, c.MessageId))));
        }

        var sort = direction == CursorDirection.Older
            ? Builders<MessageDocument>.Sort.Descending(m => m.CreatedAtUtc).Descending(m => m.Id)
            : Builders<MessageDocument>.Sort.Ascending(m => m.CreatedAtUtc).Ascending(m => m.Id);

        var docs = await context.Messages
            .Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<Guid>> MarkDeliveredAsync(
        string conversationId,
        IReadOnlyList<Guid> messageIds,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        if (messageIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var fb = Builders<MessageDocument>.Filter;
        var filter = fb.And(
            fb.Eq(m => m.ConversationId, conversationId),
            fb.In(m => m.Id, messageIds),
            fb.Eq(m => m.State, MessageState.Sent));

        // Capture the IDs that will actually transition before issuing the update.
        var transitioningIds = await context.Messages
            .Find(filter)
            .Project(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (transitioningIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var update = Builders<MessageDocument>.Update
            .Set(m => m.State, MessageState.Delivered)
            .Set(m => m.DeliveredAtUtc, deliveredAtUtc.UtcDateTime);

        await context.Messages
            .UpdateManyAsync(
                fb.And(fb.Eq(m => m.ConversationId, conversationId), fb.In(m => m.Id, transitioningIds)),
                update,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return transitioningIds;
    }

    public async Task<Guid?> MarkReadUpToAsync(
        string conversationId,
        Guid upToMessageId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        var anchor = await context.Messages
            .Find(m => m.Id == upToMessageId && m.ConversationId == conversationId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (anchor is null)
        {
            return null;
        }

        var fb = Builders<MessageDocument>.Filter;
        var anchorTs = anchor.CreatedAtUtc;
        var filter = fb.And(
            fb.Eq(m => m.ConversationId, conversationId),
            fb.Ne(m => m.State, MessageState.Read),
            fb.Ne(m => m.State, MessageState.Deleted),
            fb.Or(
                fb.Lt(m => m.CreatedAtUtc, anchorTs),
                fb.And(fb.Eq(m => m.CreatedAtUtc, anchorTs), fb.Lte(m => m.Id, upToMessageId))));

        var transitioned = await context.Messages
            .Find(filter)
            .Project(m => new { m.Id, m.DeliveredAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (transitioned.Count == 0)
        {
            return null;
        }

        var ts = readAtUtc.UtcDateTime;

        // Set Delivered timestamp where it's not yet set, then set Read state and ReadAt.
        var deliveredUpdate = Builders<MessageDocument>.Update
            .Set(m => m.DeliveredAtUtc, ts);
        await context.Messages
            .UpdateManyAsync(
                fb.And(
                    fb.Eq(m => m.ConversationId, conversationId),
                    fb.In(m => m.Id, transitioned.Where(t => t.DeliveredAtUtc is null).Select(t => t.Id).ToList()),
                    fb.Eq(m => m.DeliveredAtUtc, default(DateTime?))),
                deliveredUpdate,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var readUpdate = Builders<MessageDocument>.Update
            .Set(m => m.State, MessageState.Read)
            .Set(m => m.ReadAtUtc, ts);
        await context.Messages
            .UpdateManyAsync(
                fb.And(fb.Eq(m => m.ConversationId, conversationId), fb.In(m => m.Id, transitioned.Select(t => t.Id).ToList())),
                readUpdate,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return upToMessageId;
    }
}
