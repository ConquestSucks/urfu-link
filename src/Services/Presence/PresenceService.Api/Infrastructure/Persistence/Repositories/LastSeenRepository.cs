using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using Urfu.Link.Services.Presence.Domain.Aggregates;
using Urfu.Link.Services.Presence.Domain.Interfaces;

namespace Urfu.Link.Services.Presence.Infrastructure.Persistence.Repositories;

public sealed class LastSeenRepository(
    PresenceDbContext dbContext,
    IOutboxWriter outboxWriter,
    string serviceName) : ILastSeenRepository
{
    public async Task<LastSeen?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.LastSeens
            .FindAsync([userId], cancellationToken)
            .ConfigureAwait(false);
    }

    public void Upsert(LastSeen lastSeen)
    {
        ArgumentNullException.ThrowIfNull(lastSeen);
        if (dbContext.Entry(lastSeen).State == EntityState.Detached)
        {
            dbContext.LastSeens.Add(lastSeen);
        }

        // Write-through: every snapshot update also appends an audit row to the
        // partitioned history table so analytics can answer "when was this user online
        // during March?" without joining Kafka logs. The history table is partitioned by
        // RecordedAtUtc so this insert lands directly in the current month partition.
        dbContext.LastSeenHistory.Add(LastSeenHistoryEntry.Record(
            userId: lastSeen.UserId,
            lastSeenAtUtc: lastSeen.LastSeenAt,
            platform: lastSeen.LastPlatform,
            recordedAtUtc: DateTimeOffset.UtcNow));
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<LastSeen>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var domainEvent in domainEvents)
        {
            await DispatchEventAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask DispatchEventAsync(IIntegrationEvent domainEvent, CancellationToken cancellationToken)
    {
        var envelope = new IntegrationEnvelope<IIntegrationEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            Source: serviceName,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: domainEvent);

        await outboxWriter.EnqueueAsync(KafkaTopicNames.PresenceEvents, envelope, cancellationToken).ConfigureAwait(false);
    }
}
