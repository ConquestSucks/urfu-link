using System.Diagnostics;
using DisciplineService.Api.Domain;
using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;

namespace DisciplineService.Api.Infrastructure.Persistence;

public sealed class DisciplineRepository(
    DisciplineDbContext dbContext,
    IOutboxWriter outboxWriter,
    ServiceProfile serviceProfile) : IDisciplineRepository
{
    public Task<Discipline?> GetByIdAsync(Guid disciplineId, CancellationToken cancellationToken)
    {
        return dbContext.Disciplines
            .Include(d => d.Enrollments)
            .FirstOrDefaultAsync(d => d.Id == disciplineId, cancellationToken);
    }

    public Task<bool> CodeExistsAsync(string code, Guid? excludeDisciplineId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var trimmed = code.Trim();
        var query = dbContext.Disciplines
            .AsNoTracking()
            .Where(d => d.Code == trimmed);
        if (excludeDisciplineId.HasValue)
        {
            query = query.Where(d => d.Id != excludeDisciplineId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public void Add(Discipline discipline)
    {
        ArgumentNullException.ThrowIfNull(discipline);
        dbContext.Disciplines.Add(discipline);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<Discipline>()
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
        var envelope = CreateEnvelope(domainEvent);
        await outboxWriter.EnqueueAsync(serviceProfile.TopicName, envelope, cancellationToken).ConfigureAwait(false);
    }

    private IntegrationEnvelope<IIntegrationEvent> CreateEnvelope(IIntegrationEvent payload)
    {
        return new IntegrationEnvelope<IIntegrationEvent>(
            MessageId: Guid.NewGuid(),
            TraceId: Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N"),
            Source: serviceProfile.ServiceName,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: payload);
    }
}
