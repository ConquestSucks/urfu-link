using MongoDB.Bson;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Interfaces;
using Urfu.Link.Services.Chat.Infrastructure.Persistence.Documents;

namespace Urfu.Link.Services.Chat.Infrastructure.Persistence;

internal sealed class ThreadSubscriptionRepository(ChatMongoContext context) : IThreadSubscriptionRepository
{
    public async Task<ThreadSubscriptionUpsertResult> UpsertAsync(
        ThreadSubscription subscription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var filter = Builders<ThreadSubscriptionDocument>.Filter.And(
            Builders<ThreadSubscriptionDocument>.Filter.Eq(d => d.RootMessageId, subscription.RootMessageId),
            Builders<ThreadSubscriptionDocument>.Filter.Eq(d => d.UserId, subscription.UserId));

        // Pipeline upsert: $max on reason and lastActivityAtUtc enforces escalation-only
        // semantics atomically. Reason is stored as its enum string ("Manual"/"Mentioned"/"Replied")
        // — by design these names sort lexicographically in the same order as the enum's priority,
        // so $max on the string yields the higher-priority value.
        var pipeline = new BsonDocument[]
        {
            new("$set", new BsonDocument
            {
                { "rootMessageId", new BsonBinaryData(subscription.RootMessageId, GuidRepresentation.Standard) },
                { "userId", new BsonBinaryData(subscription.UserId, GuidRepresentation.Standard) },
                {
                    "subscribedAtUtc",
                    new BsonDocument("$ifNull", new BsonArray { "$subscribedAtUtc", subscription.SubscribedAtUtc.UtcDateTime })
                },
                {
                    "reason",
                    new BsonDocument("$max", new BsonArray { "$reason", subscription.Reason.ToString() })
                },
                {
                    "lastActivityAtUtc",
                    new BsonDocument("$max", new BsonArray { "$lastActivityAtUtc", subscription.LastActivityAtUtc.UtcDateTime })
                },
            })
        };

        var update = new BsonDocumentStagePipelineDefinition<ThreadSubscriptionDocument, ThreadSubscriptionDocument>(pipeline);
        var options = new FindOneAndUpdateOptions<ThreadSubscriptionDocument, ThreadSubscriptionDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.Before,
        };

        var before = await context.ThreadSubscriptions
            .FindOneAndUpdateAsync(filter, update, options, cancellationToken)
            .ConfigureAwait(false);

        var wasCreated = before is null;
        var reasonEscalated = before is not null && (int)before.Reason < (int)subscription.Reason;
        return new ThreadSubscriptionUpsertResult(wasCreated, reasonEscalated);
    }

    public async Task<bool> RemoveAsync(Guid rootMessageId, Guid userId, CancellationToken cancellationToken)
    {
        var result = await context.ThreadSubscriptions
            .DeleteOneAsync(
                d => d.RootMessageId == rootMessageId && d.UserId == userId,
                cancellationToken)
            .ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    public async Task<IReadOnlyList<Guid>> GetSubscriberIdsAsync(Guid rootMessageId, CancellationToken cancellationToken)
    {
        var ids = await context.ThreadSubscriptions
            .Find(d => d.RootMessageId == rootMessageId)
            .Project(d => d.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids;
    }

    public async Task<bool> IsSubscribedAsync(Guid rootMessageId, Guid userId, CancellationToken cancellationToken)
    {
        var count = await context.ThreadSubscriptions
            .CountDocumentsAsync(
                d => d.RootMessageId == rootMessageId && d.UserId == userId,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return count > 0;
    }

    public async Task<long> TouchActivityForRootAsync(
        Guid rootMessageId,
        DateTimeOffset atUtc,
        CancellationToken cancellationToken)
    {
        var pipeline = new BsonDocument[]
        {
            new("$set", new BsonDocument(
                "lastActivityAtUtc",
                new BsonDocument("$max", new BsonArray { "$lastActivityAtUtc", atUtc.UtcDateTime })))
        };
        var update = new BsonDocumentStagePipelineDefinition<ThreadSubscriptionDocument, ThreadSubscriptionDocument>(pipeline);

        var filter = Builders<ThreadSubscriptionDocument>.Filter.Eq(d => d.RootMessageId, rootMessageId);
        var result = await context.ThreadSubscriptions
            .UpdateManyAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount;
    }

    public async Task<IReadOnlyList<ThreadSubscription>> ListUserActiveAsync(
        Guid userId,
        ThreadActivityCursor? cursor,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return Array.Empty<ThreadSubscription>();
        }

        var fb = Builders<ThreadSubscriptionDocument>.Filter;
        var filter = fb.Eq(d => d.UserId, userId);

        if (cursor is { } c)
        {
            var ts = c.LastActivityAtUtc.UtcDateTime;
            // Strict descending paging keyed on (lastActivityAtUtc, rootMessageId): take rows
            // older than the cursor, breaking ties by RootMessageId desc.
            var cursorFilter = fb.Or(
                fb.Lt(d => d.LastActivityAtUtc, ts),
                fb.And(
                    fb.Eq(d => d.LastActivityAtUtc, ts),
                    fb.Lt(d => d.RootMessageId, c.RootMessageId)));
            filter = fb.And(filter, cursorFilter);
        }

        var docs = await context.ThreadSubscriptions
            .Find(filter)
            .Sort(Builders<ThreadSubscriptionDocument>.Sort
                .Descending(d => d.LastActivityAtUtc)
                .Descending(d => d.RootMessageId))
            .Limit(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return docs.Select(d => d.ToDomain()).ToList();
    }
}
