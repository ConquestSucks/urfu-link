using MongoDB.Bson;
using MongoDB.Driver;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;
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

    public async Task<bool> AddPinnedMessageAsync(
        string conversationId,
        Guid messageId,
        int maxPinned,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPinned);

        // Atomic precondition: conversation exists, messageId not yet pinned, and
        // pinnedMessageIds size is below the cap. $ifNull guards against a missing field
        // (rows persisted before the pinned-array existed).
        var filter = new BsonDocument
        {
            { "_id", conversationId },
            {
                "pinnedMessageIds",
                new BsonDocument("$ne", new BsonBinaryData(messageId, GuidRepresentation.Standard))
            },
            {
                "$expr",
                new BsonDocument("$lt", new BsonArray
                {
                    new BsonDocument("$size",
                        new BsonDocument("$ifNull", new BsonArray { "$pinnedMessageIds", new BsonArray() })),
                    maxPinned,
                })
            },
        };

        var update = Builders<ConversationDocument>.Update.AddToSet(c => c.PinnedMessageIds, messageId);
        var result = await context.Conversations
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> RemovePinnedMessageAsync(
        string conversationId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ConversationDocument>.Filter.And(
            Builders<ConversationDocument>.Filter.Eq(c => c.Id, conversationId),
            Builders<ConversationDocument>.Filter.AnyEq(c => c.PinnedMessageIds, messageId));
        var update = Builders<ConversationDocument>.Update.Pull(c => c.PinnedMessageIds, messageId);
        var result = await context.Conversations
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<IReadOnlyList<string>> GetUserConversationIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ConversationDocument>.Filter.AnyEq(c => c.Participants, userId);
        var ids = await context.Conversations
            .Find(filter)
            .Project(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return ids;
    }

    public async Task<IReadOnlyList<Conversation>> GetByIdsAsync(
        IReadOnlyList<string> conversationIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversationIds);
        if (conversationIds.Count == 0)
        {
            return Array.Empty<Conversation>();
        }

        var filter = Builders<ConversationDocument>.Filter.In(c => c.Id, conversationIds);
        var docs = await context.Conversations
            .Find(filter)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task<Conversation?> GetByDisciplineIdAsync(Guid disciplineId, CancellationToken cancellationToken)
    {
        var doc = await context.Conversations
            .Find(c => c.DisciplineId == disciplineId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return doc?.ToDomain();
    }

    public async Task<bool> AddParticipantAsync(
        string conversationId,
        Guid userId,
        ParticipantRole role,
        CancellationToken cancellationToken)
    {
        // Step 1 (idempotent): if a role entry for this user already exists, replace
        // it in-place atomically using the positional $ operator. The role array and
        // participants array stay consistent because we never delete then re-insert.
        var roleString = role.ToString();
        var existingFilter = Builders<ConversationDocument>.Filter.And(
            Builders<ConversationDocument>.Filter.Eq(c => c.Id, conversationId),
            Builders<ConversationDocument>.Filter.Eq("participantRoles.userId", userId));
        var setRole = Builders<ConversationDocument>.Update.Set(
            "participantRoles.$.role",
            roleString);
        var setResult = await context.Conversations
            .UpdateOneAsync(existingFilter, setRole, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (setResult.MatchedCount > 0)
        {
            // The user was already a (possibly differently-roled) participant; we
            // just refreshed the role. Return false so the caller doesn't double-publish.
            return false;
        }

        // Step 2 (atomic): brand-new participant — push to both arrays in a single
        // UpdateOne so a process crash mid-update can't leave one array stale.
        // The participantRoles.userId guard keeps the call idempotent under retries.
        var freshFilter = Builders<ConversationDocument>.Filter.And(
            Builders<ConversationDocument>.Filter.Eq(c => c.Id, conversationId),
            Builders<ConversationDocument>.Filter.Ne(
                "participants",
                new BsonBinaryData(userId, GuidRepresentation.Standard)),
            Builders<ConversationDocument>.Filter.Ne(
                "participantRoles.userId",
                userId));
        var addUpdate = Builders<ConversationDocument>.Update.Combine(
            Builders<ConversationDocument>.Update.AddToSet(c => c.Participants, userId),
            Builders<ConversationDocument>.Update.Push(
                "participantRoles",
                new ParticipantRoleEntry(userId, role)));
        var addResult = await context.Conversations
            .UpdateOneAsync(freshFilter, addUpdate, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return addResult.ModifiedCount > 0;
    }

    public async Task<bool> RemoveParticipantAsync(
        string conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var update = Builders<ConversationDocument>.Update
            .Pull(c => c.Participants, userId)
            .PullFilter(
                "participantRoles",
                Builders<ParticipantRoleEntry>.Filter.Eq(e => e.UserId, userId));
        var result = await context.Conversations
            .UpdateOneAsync(c => c.Id == conversationId, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> ChangeParticipantRoleAsync(
        string conversationId,
        Guid userId,
        ParticipantRole newRole,
        CancellationToken cancellationToken)
    {
        // Single atomic Set via the positional $ operator: replaces the role of an
        // existing entry without ever leaving the participantRoles array empty.
        var roleString = newRole.ToString();
        var filter = Builders<ConversationDocument>.Filter.And(
            Builders<ConversationDocument>.Filter.Eq(c => c.Id, conversationId),
            Builders<ConversationDocument>.Filter.AnyEq(c => c.Participants, userId),
            Builders<ConversationDocument>.Filter.Eq("participantRoles.userId", userId));
        var update = Builders<ConversationDocument>.Update.Set(
            "participantRoles.$.role",
            roleString);
        var result = await context.Conversations
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.ModifiedCount > 0)
        {
            return true;
        }

        // Fallback for participants that pre-date role tracking: no entry yet, but
        // the user IS a participant — push the role atomically. The participantRoles
        // guard makes the operation idempotent so retries can't insert duplicates.
        var legacyFilter = Builders<ConversationDocument>.Filter.And(
            Builders<ConversationDocument>.Filter.Eq(c => c.Id, conversationId),
            Builders<ConversationDocument>.Filter.AnyEq(c => c.Participants, userId),
            Builders<ConversationDocument>.Filter.Ne("participantRoles.userId", userId));
        var legacyPush = Builders<ConversationDocument>.Update.Push(
            "participantRoles",
            new ParticipantRoleEntry(userId, newRole));
        var legacyResult = await context.Conversations
            .UpdateOneAsync(legacyFilter, legacyPush, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return legacyResult.ModifiedCount > 0;
    }

    public async Task<bool> ArchiveAsync(
        string conversationId,
        DateTimeOffset archivedAtUtc,
        CancellationToken cancellationToken)
    {
        var filter = Builders<ConversationDocument>.Filter.And(
            Builders<ConversationDocument>.Filter.Eq(c => c.Id, conversationId),
            Builders<ConversationDocument>.Filter.Eq(c => c.ArchivedAtUtc, (DateTime?)null));
        var update = Builders<ConversationDocument>.Update.Set(c => c.ArchivedAtUtc, archivedAtUtc.UtcDateTime);
        var result = await context.Conversations
            .UpdateOneAsync(filter, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> UpdateMetadataAsync(
        string conversationId,
        string? title,
        Guid? coverAssetId,
        CancellationToken cancellationToken)
    {
        var update = Builders<ConversationDocument>.Update
            .Set(c => c.Title, title)
            .Set(c => c.CoverAssetId, coverAssetId);
        var result = await context.Conversations
            .UpdateOneAsync(c => c.Id == conversationId, update, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ModifiedCount > 0;
    }
}
