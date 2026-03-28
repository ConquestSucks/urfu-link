using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;
using UserService.Api.Domain;
using UserService.Api.Domain.Interfaces;

namespace UserService.Api.Infrastructure.Persistence;

public sealed class UserRepository(
    UserDbContext dbContext,
    IOutboxWriter outboxWriter,
    ServiceProfile serviceProfile) : IUserRepository
{
    public async Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.UserProfiles
            .FindAsync([userId], cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(UserProfile user)
    {
        dbContext.UserProfiles.Add(user);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<UserProfile>()
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
