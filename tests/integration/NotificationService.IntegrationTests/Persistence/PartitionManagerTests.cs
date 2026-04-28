using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.IntegrationTests.Infrastructure;
using Urfu.Link.Services.Notification.Infrastructure.Persistence;

namespace NotificationService.IntegrationTests.Persistence;

/// <summary>
/// Integration tests for <see cref="PartitionManager"/>. <c>notifications.notifications</c>
/// is partitioned by month — writes that fall outside the existing partitions fail with
/// <c>no partition found for given row</c>, so the rolling-window worker must be reliable.
/// Verifies create-and-drop semantics against a real Postgres instance.
/// </summary>
[Collection(IntegrationCollection.Name)]
public sealed class PartitionManagerTests(NotificationServiceFactory factory) : IAsyncLifetime
{
    private readonly NotificationServiceFactory _factory = factory;

    public Task InitializeAsync() => _factory.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EnsureAsync_creates_partition_for_arbitrary_future_month()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<PartitionManager>();
        var futureMonth = new YearMonth(2099, 12);

        try
        {
            await manager.EnsureAsync(futureMonth, CancellationToken.None);

            var partitions = await manager.ListAsync(CancellationToken.None);
            partitions.Should().Contain(futureMonth,
                "EnsureAsync just created a partition for the requested month.");
        }
        finally
        {
            await manager.DropAsync(futureMonth, CancellationToken.None);
        }
    }

    [Fact]
    public async Task EnsureAsync_is_idempotent_across_repeated_calls()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<PartitionManager>();
        var month = new YearMonth(2098, 6);

        try
        {
            await manager.EnsureAsync(month, CancellationToken.None);
            await manager.EnsureAsync(month, CancellationToken.None);
            await manager.EnsureAsync(month, CancellationToken.None);

            var partitions = await manager.ListAsync(CancellationToken.None);
            partitions.Count(m => m == month).Should().Be(1,
                "creating the same partition repeatedly must not produce duplicates.");
        }
        finally
        {
            await manager.DropAsync(month, CancellationToken.None);
        }
    }

    [Fact]
    public async Task DropAsync_removes_partition_so_subsequent_list_excludes_it()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<PartitionManager>();
        var month = new YearMonth(2097, 3);

        await manager.EnsureAsync(month, CancellationToken.None);
        (await manager.ListAsync(CancellationToken.None)).Should().Contain(month);

        await manager.DropAsync(month, CancellationToken.None);

        (await manager.ListAsync(CancellationToken.None)).Should().NotContain(month);
    }

    [Fact]
    public async Task DropAsync_is_idempotent_for_non_existent_partition()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<PartitionManager>();
        var nonExistent = new YearMonth(2090, 1);

        // Should not throw — the worker may try to drop a partition twice in a row
        // (e.g. after a crash) and that must be safe.
        await manager.DropAsync(nonExistent, CancellationToken.None);
        await manager.DropAsync(nonExistent, CancellationToken.None);
    }
}
