using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Domain.ValueObjects;
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

    public async Task<IReadOnlyList<Message>> GetByIdsAsync(
        string conversationId,
        IReadOnlyList<Guid> messageIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        if (messageIds.Count == 0)
        {
            return Array.Empty<Message>();
        }

        var fb = Builders<MessageDocument>.Filter;
        var filter = fb.And(
            fb.Eq(m => m.ConversationId, conversationId),
            fb.In(m => m.Id, messageIds));

        var docs = await context.Messages
            .Find(filter)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task<IReadOnlyList<Message>> GetManyAsync(
        IReadOnlyList<Guid> messageIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(messageIds);
        if (messageIds.Count == 0)
        {
            return Array.Empty<Message>();
        }

        var filter = Builders<MessageDocument>.Filter.In(m => m.Id, messageIds);

        var docs = await context.Messages
            .Find(filter)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return docs.Select(d => d.ToDomain()).ToList();
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
        // Main-flow only: thread replies have threadRootId set and are surfaced exclusively via
        // ListThreadAsync. Mongo treats missing == null, so legacy documents without the field
        // continue to appear here without backfill.
        var filter = fb.And(
            fb.Eq(m => m.ConversationId, conversationId),
            fb.Eq(m => m.ThreadRootId, (Guid?)null));

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

    public async Task<IReadOnlyList<Guid>> MarkReadUpToAsync(
        string conversationId,
        Guid upToMessageId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken)
    {
        // Reading the main flow does not affect thread replies: they have their own surfacing
        // via GetThreadMessages and read transitions there are out of scope for this call. We
        // therefore reject thread reply anchors and exclude thread replies from the bulk update.
        var anchor = await context.Messages
            .Find(m => m.Id == upToMessageId
                && m.ConversationId == conversationId
                && m.ThreadRootId == null)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (anchor is null)
        {
            return Array.Empty<Guid>();
        }

        var fb = Builders<MessageDocument>.Filter;
        var anchorTs = anchor.CreatedAtUtc;
        var filter = fb.And(
            fb.Eq(m => m.ConversationId, conversationId),
            fb.Eq(m => m.ThreadRootId, (Guid?)null),
            fb.Ne(m => m.State, MessageState.Read),
            fb.Ne(m => m.State, MessageState.Deleted),
            fb.Or(
                fb.Lt(m => m.CreatedAtUtc, anchorTs),
                fb.And(fb.Eq(m => m.CreatedAtUtc, anchorTs), fb.Lte(m => m.Id, upToMessageId))));

        var transitioned = await context.Messages
            .Find(filter)
            // Sort oldest → newest so callers see ids in chronological order. The anchor sits
            // at the tail of the list.
            .Sort(Builders<MessageDocument>.Sort.Ascending(m => m.CreatedAtUtc).Ascending(m => m.Id))
            .Project(m => new { m.Id, m.DeliveredAtUtc })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (transitioned.Count == 0)
        {
            return Array.Empty<Guid>();
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

        return transitioned.Select(t => t.Id).ToList();
    }

    public async Task<bool> ApplyEditAsync(
        Guid messageId,
        string newBody,
        IReadOnlyList<Guid> mentions,
        EditHistoryEntry historyEntry,
        DateTimeOffset editedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mentions);
        ArgumentNullException.ThrowIfNull(historyEntry);

        var fb = Builders<MessageDocument>.Filter;
        var filter = fb.And(
            fb.Eq(m => m.Id, messageId),
            fb.Ne(m => m.State, MessageState.Deleted));
        var update = Builders<MessageDocument>.Update
            .Set(m => m.Body, newBody ?? string.Empty)
            .Set(m => m.Mentions, mentions.ToList())
            .Set(m => m.EditedAtUtc, editedAtUtc.UtcDateTime)
            .Push(m => m.EditHistory, EditHistoryEntryDocument.FromDomain(historyEntry));

        var result = await context.Messages
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> ApplyDeleteForEveryoneAsync(
        Guid messageId,
        Guid byUserId,
        DateTimeOffset deletedAtUtc,
        CancellationToken cancellationToken)
    {
        var fb = Builders<MessageDocument>.Filter;
        var filter = fb.And(
            fb.Eq(m => m.Id, messageId),
            fb.Ne(m => m.State, MessageState.Deleted));
        var update = Builders<MessageDocument>.Update
            .Set(m => m.State, MessageState.Deleted)
            .Set(m => m.DeletedAtUtc, deletedAtUtc.UtcDateTime)
            .Set(m => m.DeletedBy, byUserId)
            .Set(m => m.DeleteMode, DeleteMode.ForEveryone)
            .Set(m => m.Body, string.Empty)
            .Set(m => m.Attachments, new List<AttachmentDocument>())
            .Set(m => m.Reactions, new List<ReactionDocument>())
            .Set(m => m.Mentions, new List<Guid>())
            .Set(m => m.ReplyTo, (ReplyToDocument?)null)
            .Set(m => m.ForwardedFrom, (ForwardedFromDocument?)null);

        var result = await context.Messages
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> AddHiddenForAsync(Guid messageId, Guid userId, CancellationToken cancellationToken)
    {
        var fb = Builders<MessageDocument>.Filter;
        var filter = fb.And(
            fb.Eq(m => m.Id, messageId),
            fb.Not(fb.AnyEq(m => m.HiddenFor, userId)));
        var update = Builders<MessageDocument>.Update.AddToSet(m => m.HiddenFor, userId);

        var result = await context.Messages
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> AddReactionAsync(Guid messageId, Reaction reaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reaction);

        var fb = Builders<MessageDocument>.Filter;

        // Same (user, emoji) is a noop on the wire — short-circuit before issuing an update.
        // The atomic pipeline below is idempotent under concurrent same-emoji writes anyway,
        // but the early exit saves a write when the request is a duplicate.
        var sameExistsCount = await context.Messages
            .CountDocumentsAsync(
                fb.And(
                    fb.Eq(m => m.Id, messageId),
                    fb.ElemMatch(
                        m => m.Reactions,
                        r => r.UserId == reaction.UserId && r.Emoji == reaction.Emoji)),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (sameExistsCount > 0)
        {
            return false;
        }

        // Atomic pipeline update: in a single Mongo operation, drop any prior reaction by the
        // same user, then append the new one. This holds the "one emoji per (user, message)"
        // invariant under concurrent writes from the same user — pull-then-push as separate
        // updates left a window where two reactions could both be appended.
        var newReactionBson = new BsonDocument
        {
            { "userId", new BsonBinaryData(reaction.UserId, GuidRepresentation.Standard) },
            { "emoji", reaction.Emoji },
            { "createdAtUtc", reaction.CreatedAtUtc.UtcDateTime },
        };
        var setStage = new BsonDocument("$set", new BsonDocument("reactions", new BsonDocument("$concatArrays", new BsonArray
        {
            new BsonDocument("$filter", new BsonDocument
            {
                { "input", new BsonDocument("$ifNull", new BsonArray { "$reactions", new BsonArray() }) },
                { "as", "r" },
                { "cond", new BsonDocument("$ne", new BsonArray
                {
                    "$$r.userId",
                    new BsonBinaryData(reaction.UserId, GuidRepresentation.Standard),
                }) },
            }),
            new BsonArray { newReactionBson },
        })));

        PipelineDefinition<MessageDocument, MessageDocument> pipeline = new[] { setStage };
        var update = Builders<MessageDocument>.Update.Pipeline(pipeline);

        var result = await context.Messages
            .UpdateOneAsync(
                fb.And(fb.Eq(m => m.Id, messageId), fb.Ne(m => m.State, MessageState.Deleted)),
                update,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> RemoveReactionAsync(
        Guid messageId,
        Guid userId,
        string emoji,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emoji);

        var fb = Builders<MessageDocument>.Filter;
        var filter = fb.And(
            fb.Eq(m => m.Id, messageId),
            fb.ElemMatch(m => m.Reactions, r => r.UserId == userId && r.Emoji == emoji));
        var update = Builders<MessageDocument>.Update
            .PullFilter(m => m.Reactions, r => r.UserId == userId && r.Emoji == emoji);

        var result = await context.Messages
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> AddReadByAsync(Guid messageId, ReadReceipt receipt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var fb = Builders<MessageDocument>.Filter;
        var filter = fb.And(
            fb.Eq(m => m.Id, messageId),
            fb.Ne(m => m.State, MessageState.Deleted),
            fb.Not(fb.ElemMatch(m => m.ReadBy, r => r.UserId == receipt.UserId)));
        var update = Builders<MessageDocument>.Update
            .Push(m => m.ReadBy, ReadReceiptDocument.FromDomain(receipt));

        var result = await context.Messages
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<IReadOnlyList<ReadReceipt>> GetReadReceiptsAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var doc = await context.Messages
            .Find(m => m.Id == messageId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (doc is null)
        {
            return Array.Empty<ReadReceipt>();
        }
        return doc.ReadBy.Select(r => r.ToDomain()).ToList();
    }

    public async Task<bool> IncrementThreadDenormAsync(
        Guid rootMessageId,
        Guid replierUserId,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken)
    {
        var fb = Builders<MessageDocument>.Filter;
        // Filter ensures we only ever bump denorms on a non-deleted root: a thread reply has
        // threadRootId set, so excluding it prevents a reply from accidentally accumulating its
        // own children. Tombstoned roots are likewise excluded — the application layer rejects
        // replies on deleted roots and this is a defensive backstop.
        var filter = fb.And(
            fb.Eq(m => m.Id, rootMessageId),
            fb.Eq(m => m.ThreadRootId, (Guid?)null),
            fb.Ne(m => m.State, MessageState.Deleted));

        // Single atomic pipeline: $add for the counter, $setUnion for participants (dedupes the
        // replier across multiple replies), unconditional $set for the timestamp because the
        // caller always passes the new reply's timestamp.
        var pipeline = new BsonDocument[]
        {
            new("$set", new BsonDocument
            {
                {
                    "threadReplyCount",
                    new BsonDocument("$add", new BsonArray
                    {
                        new BsonDocument("$ifNull", new BsonArray { "$threadReplyCount", 0 }),
                        1,
                    })
                },
                {
                    "threadParticipants",
                    new BsonDocument("$setUnion", new BsonArray
                    {
                        new BsonDocument("$ifNull", new BsonArray { "$threadParticipants", new BsonArray() }),
                        new BsonArray { new BsonBinaryData(replierUserId, GuidRepresentation.Standard) },
                    })
                },
                { "threadLastReplyAtUtc", atUtc.UtcDateTime },
            })
        };

        PipelineDefinition<MessageDocument, MessageDocument> definition = pipeline;
        var update = Builders<MessageDocument>.Update.Pipeline(definition);

        var result = await context.Messages
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<IReadOnlyList<Message>> ListThreadAsync(
        Guid rootMessageId,
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
        var filter = fb.Eq(m => m.ThreadRootId, (Guid?)rootMessageId);

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

    public async Task<IReadOnlyList<MessageSearchHit>> SearchAsync(
        MessageSearchCriteria criteria,
        IReadOnlyList<string> allowedConversationIds,
        MessageSearchCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(allowedConversationIds);

        if (limit <= 0 || allowedConversationIds.Count == 0 || string.IsNullOrWhiteSpace(criteria.Query))
        {
            return Array.Empty<MessageSearchHit>();
        }

        // Mongo requires $text in the pipeline's first $match stage; all non-score filters share
        // that stage so the text index can drive selection. Score-based cursor matching has to
        // come after $addFields below, since $meta:textScore is only addressable as a regular
        // field once it has been projected.
        var matchStage = new BsonDocument
        {
            { "$text", new BsonDocument("$search", criteria.Query) },
            { "conversationId", new BsonDocument("$in", new BsonArray(allowedConversationIds)) },
            { "threadRootId", BsonNull.Value },
        };

        if (criteria.SenderId is { } senderId)
        {
            matchStage.Add("senderId", new BsonBinaryData(senderId, GuidRepresentation.Standard));
        }

        if (criteria.DateFrom.HasValue || criteria.DateTo.HasValue)
        {
            var range = new BsonDocument();
            if (criteria.DateFrom is { } from)
            {
                range.Add("$gte", from.UtcDateTime);
            }
            if (criteria.DateTo is { } to)
            {
                range.Add("$lte", to.UtcDateTime);
            }
            matchStage.Add("createdAtUtc", range);
        }

        if (criteria.HasAttachments == true)
        {
            matchStage.Add("attachments.0", new BsonDocument("$exists", true));
        }
        else if (criteria.HasAttachments == false)
        {
            matchStage.Add("$or", new BsonArray
            {
                new BsonDocument("attachments", new BsonDocument("$exists", false)),
                new BsonDocument("attachments", new BsonDocument("$size", 0)),
            });
        }

        if (criteria.AttachmentType is { } type)
        {
            // AttachmentDocument.Type is serialized as a string via BsonRepresentation.String.
            matchStage.Add("attachments.type", type.ToString());
        }

        var stages = new List<BsonDocument>
        {
            new("$match", matchStage),
            new("$addFields", new BsonDocument("_score", new BsonDocument("$meta", "textScore"))),
        };

        if (cursor is { } c)
        {
            // Keyset predicate for descending sort on (_score, createdAtUtc, _id): documents come
            // after the cursor position iff one of the three lexicographic comparisons holds.
            var cursorTs = c.CreatedAtUtc.UtcDateTime;
            stages.Add(new("$match", new BsonDocument("$or", new BsonArray
            {
                new BsonDocument("_score", new BsonDocument("$lt", c.Score)),
                new BsonDocument
                {
                    { "_score", c.Score },
                    { "createdAtUtc", new BsonDocument("$lt", cursorTs) },
                },
                new BsonDocument
                {
                    { "_score", c.Score },
                    { "createdAtUtc", cursorTs },
                    { "_id", new BsonDocument("$lt", new BsonBinaryData(c.MessageId, GuidRepresentation.Standard)) },
                },
            })));
        }

        stages.Add(new("$sort", new BsonDocument
        {
            { "_score", -1 },
            { "createdAtUtc", -1 },
            { "_id", -1 },
        }));
        stages.Add(new("$limit", limit));

        PipelineDefinition<MessageDocument, BsonDocument> pipeline = stages;

        var raw = await context.Messages
            .Aggregate(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hits = new List<MessageSearchHit>(raw.Count);
        foreach (var doc in raw)
        {
            var score = doc.GetValue("_score", BsonDouble.Create(0.0)).ToDouble();
            doc.Remove("_score");
            var msgDoc = BsonSerializer.Deserialize<MessageDocument>(doc);
            hits.Add(new MessageSearchHit(msgDoc.ToDomain(), score));
        }
        return hits;
    }
}
