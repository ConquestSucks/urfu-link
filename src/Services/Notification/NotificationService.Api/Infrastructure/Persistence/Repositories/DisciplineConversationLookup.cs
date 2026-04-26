using Microsoft.EntityFrameworkCore;
using Urfu.Link.Services.Notification.Domain.Aggregates;
using Urfu.Link.Services.Notification.Domain.Interfaces;

namespace Urfu.Link.Services.Notification.Infrastructure.Persistence.Repositories;

public sealed class DisciplineConversationLookup(NotificationDbContext db, TimeProvider timeProvider) : IDisciplineConversationLookup
{
    public Task<bool> IsDisciplineConversationAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        return db.DisciplineConversations
            .AsNoTracking()
            .AnyAsync(x => x.ConversationId == conversationId, cancellationToken);
    }

    public async Task RegisterAsync(string conversationId, Guid disciplineId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);

        var existing = await db.DisciplineConversations
            .FirstOrDefaultAsync(x => x.ConversationId == conversationId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        db.DisciplineConversations.Add(DisciplineConversation.Create(conversationId, disciplineId, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
