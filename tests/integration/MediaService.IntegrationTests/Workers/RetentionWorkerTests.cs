using FluentAssertions;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Domain.Events;
using MediaService.Api.Infrastructure.Persistence;
using MediaService.Api.Workers;
using MediaService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MediaService.IntegrationTests.Workers;

[Collection(IntegrationCollection.Name)]
public sealed class RetentionWorkerTests : IAsyncLifetime
{
    private readonly MediaServiceFactory _factory;

    public RetentionWorkerTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Sweep_HardDeletesAssetsBeyondRetentionTtl_AndPublishesEvent()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);
        await SoftDeleteAsync(ownerId, assetId);
        await BackdateDeletedAtAsync(assetId, TimeSpan.FromDays(31));
        _factory.OutboxWriter.Clear();

        using var worker = CreateWorker();
        await worker.SweepAsync(CancellationToken.None);

        await using var scope = _factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var asset = await ctx.Assets.AsNoTracking().SingleAsync(a => a.Id == assetId);
        asset.State.Should().Be(AssetState.HardDeleted,
            "an asset soft-deleted longer than the retention TTL must be hard-deleted by the sweep");

        _factory.OutboxWriter.Published
            .Select(p => p.Payload)
            .OfType<MediaAssetHardDeletedEvent>()
            .Should().Contain(ev => ev.AssetId == assetId,
                "hard delete must publish MediaAssetHardDeletedEvent so consumers can purge cached metadata");
    }

    [Fact]
    public async Task Sweep_LeavesRecentlyDeletedAssetsUntouched()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);
        await SoftDeleteAsync(ownerId, assetId);
        // No backdate -- DeletedAtUtc is "now", well within the 30-day TTL.

        using var worker = CreateWorker();
        await worker.SweepAsync(CancellationToken.None);

        await using var scope = _factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var asset = await ctx.Assets.AsNoTracking().SingleAsync(a => a.Id == assetId);
        asset.State.Should().Be(AssetState.Deleted,
            "recently soft-deleted assets must stay in the retention window");
    }

    private RetentionWorker CreateWorker()
        => new(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RetentionWorkerOptions
            {
                SoftDeleteTtl = TimeSpan.FromDays(30),
                SweepInterval = TimeSpan.FromHours(24),
                BatchLimit = 500,
            }),
            NullLogger<RetentionWorker>.Instance);

    private async Task SoftDeleteAsync(Guid ownerId, Guid assetId)
    {
        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.DeleteAsync($"/api/v1/media/{assetId}");
        response.EnsureSuccessStatusCode();
    }

    private async Task BackdateDeletedAtAsync(Guid assetId, TimeSpan howLongAgo)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var pastTime = DateTimeOffset.UtcNow - howLongAgo;
        await ctx.Assets.Where(a => a.Id == assetId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.DeletedAtUtc, pastTime));
    }
}
