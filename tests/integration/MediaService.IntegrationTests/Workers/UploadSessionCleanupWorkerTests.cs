using FluentAssertions;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Infrastructure.Persistence;
using MediaService.Api.Workers;
using MediaService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MediaService.IntegrationTests.Workers;

[Collection(IntegrationCollection.Name)]
public sealed class UploadSessionCleanupWorkerTests : IAsyncLifetime
{
    private readonly MediaServiceFactory _factory;

    public UploadSessionCleanupWorkerTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Sweep_ExpiredSessionForInitiatedAsset_DeletesObjectAndMarksFailed()
    {
        var ownerId = Guid.NewGuid();
        var init = await TestAssetBuilder.InitAsync(_factory, ownerId);

        await ExpireSessionAsync(init.AssetId);
        using var worker = CreateWorker();

        await worker.SweepAsync(CancellationToken.None);

        await using var assertScope = _factory.Services.CreateAsyncScope();
        var ctx = assertScope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var asset = await ctx.Assets.AsNoTracking().SingleAsync(a => a.Id == init.AssetId);
        asset.State.Should().Be(AssetState.Failed,
            "expired upload session for an Initiated asset must transition the asset to Failed");

        var session = await ctx.UploadSessions.AsNoTracking().SingleOrDefaultAsync(s => s.AssetId == init.AssetId);
        session.Should().BeNull("the expired session must be removed by the sweep");
    }

    [Fact]
    public async Task Sweep_LeavesNonExpiredSessionsUntouched()
    {
        var ownerId = Guid.NewGuid();
        var init = await TestAssetBuilder.InitAsync(_factory, ownerId);
        // Session is created with a 30-min TTL — far in the future.
        using var worker = CreateWorker();

        await worker.SweepAsync(CancellationToken.None);

        await using var assertScope = _factory.Services.CreateAsyncScope();
        var ctx = assertScope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var asset = await ctx.Assets.AsNoTracking().SingleAsync(a => a.Id == init.AssetId);
        asset.State.Should().Be(AssetState.Initiated, "non-expired session must not transition the asset");
        var session = await ctx.UploadSessions.AsNoTracking().SingleOrDefaultAsync(s => s.AssetId == init.AssetId);
        session.Should().NotBeNull("non-expired session must remain in the table");
    }

    [Fact]
    public async Task Sweep_CompletedSessionForUploadedAsset_LeavesAssetAndSessionAlone()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        // Even after artificially backdating the session's ExpiresAtUtc, the
        // sweep must skip it because IsCompleted filters it out of GetExpiredAsync.
        await ExpireSessionAsync(assetId);
        using var worker = CreateWorker();

        await worker.SweepAsync(CancellationToken.None);

        await using var assertScope = _factory.Services.CreateAsyncScope();
        var ctx = assertScope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var asset = await ctx.Assets.AsNoTracking().SingleAsync(a => a.Id == assetId);
        asset.State.Should().Be(AssetState.Uploaded,
            "Uploaded asset must remain Uploaded — the cleanup worker never touches assets in this state");

        var session = await ctx.UploadSessions.AsNoTracking().SingleOrDefaultAsync(s => s.AssetId == assetId);
        session.Should().NotBeNull("completed sessions are never picked up by GetExpiredAsync");
        session!.IsCompleted.Should().BeTrue();
    }

    private UploadSessionCleanupWorker CreateWorker()
        => new(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<UploadSessionCleanupWorker>.Instance);

    private async Task ExpireSessionAsync(Guid assetId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
        var pastTime = DateTimeOffset.UtcNow.AddMinutes(-1);
        await ctx.UploadSessions.Where(s => s.AssetId == assetId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAtUtc, pastTime));
    }
}
