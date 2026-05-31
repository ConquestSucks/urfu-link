using DisciplineService.Api.Domain;
using DisciplineService.Api.Domain.Aggregates;
using DisciplineService.Api.Domain.Interfaces;
using DisciplineService.Api.Infrastructure.Outbox;
using DisciplineService.Api.Infrastructure.Persistence;
using DisciplineService.IntegrationTests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Urfu.Link.BuildingBlocks.Contracts.Integration;
using Urfu.Link.BuildingBlocks.Outbox;

namespace DisciplineService.IntegrationTests.Outbox;

/// <summary>
/// Verifies the transactional-outbox guarantees that the rest of the suite cannot
/// exercise because the shared <c>FakeOutboxWriter</c> short-circuits the DB path.
/// These tests build a fresh DI scope with the real <see cref="EfOutboxWriter"/>
/// against the same TestContainers Postgres so we can assert that:
///   1. Outbox rows commit atomically with the aggregate change.
///   2. Outbox rows roll back when the transaction is aborted.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class TransactionalOutboxTests : IAsyncLifetime
{
    private readonly DisciplineServiceFactory _factory;

    public TransactionalOutboxTests(DisciplineServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveChangesAsync_CommitsOutboxRowsAlongsideAggregate()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DisciplineDbContext>();
        var profile = scope.ServiceProvider.GetRequiredService<ServiceProfile>();
        var writer = new EfOutboxWriter(dbContext);
        var repository = new DisciplineRepository(dbContext, writer, profile);

        var discipline = Discipline.CreateNew(
            "CS-OUTBOX",
            "Transactional outbox",
            description: null,
            "2026-spring",
            ownerTeacherId: Guid.NewGuid(),
            coverAssetId: null,
            initiatedBy: Guid.NewGuid());

        repository.Add(discipline);
        await repository.SaveChangesAsync(CancellationToken.None);

        await using var assertScope = _factory.Services.CreateAsyncScope();
        var verify = assertScope.ServiceProvider.GetRequiredService<DisciplineDbContext>();

        (await verify.Disciplines.AnyAsync(d => d.Id == discipline.Id)).Should().BeTrue();
        var outboxRows = await verify.OutboxMessages
            .Where(m => m.PublishedAtUtc == null)
            .ToListAsync();
        outboxRows.Should().HaveCount(3);
        outboxRows.Select(r => r.EventType).Should().Contain([
            "discipline.created.v1",
            "discipline.user_enrolled.v1",
            "discipline.subgroup_created.v1",
        ]);
    }

    [Fact]
    public async Task SaveChangesAsync_RollsBackOutboxWhenAggregateInsertFails()
    {
        // Seed an existing discipline so the second insert collides on the unique
        // code constraint and the whole SaveChanges aborts. Both rows must vanish.
        var existing = Discipline.CreateNew(
            "CS-DUP",
            "First",
            null,
            "2026-spring",
            Guid.NewGuid(),
            null,
            Guid.NewGuid());

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var dbContext = seed.ServiceProvider.GetRequiredService<DisciplineDbContext>();
            var profile = seed.ServiceProvider.GetRequiredService<ServiceProfile>();
            var writer = new EfOutboxWriter(dbContext);
            var repository = new DisciplineRepository(dbContext, writer, profile);
            repository.Add(existing);
            await repository.SaveChangesAsync(CancellationToken.None);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DisciplineDbContext>();
        var sp = scope.ServiceProvider.GetRequiredService<ServiceProfile>();
        var conflictWriter = new EfOutboxWriter(ctx);
        var conflictRepo = new DisciplineRepository(ctx, conflictWriter, sp);

        var conflicting = Discipline.CreateNew(
            "CS-DUP",
            "Second with the same code",
            null,
            "2026-spring",
            Guid.NewGuid(),
            null,
            Guid.NewGuid());
        conflictRepo.Add(conflicting);

        await Assert.ThrowsAsync<DbUpdateException>(() => conflictRepo.SaveChangesAsync(CancellationToken.None));

        await using var assertScope = _factory.Services.CreateAsyncScope();
        var verify = assertScope.ServiceProvider.GetRequiredService<DisciplineDbContext>();
        var conflictingId = conflicting.Id.ToString("D");
        var rows = await verify.OutboxMessages
            .AsNoTracking()
            .Select(m => m.Payload)
            .ToListAsync();
        rows.Count(p => p.Contains(conflictingId, StringComparison.Ordinal))
            .Should().Be(0, "outbox rows should roll back together with the failed insert");
    }
}
