using FluentAssertions;
using MediaService.Api.Infrastructure.Persistence;
using MediaService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaService.IntegrationTests.Persistence;

[Collection(IntegrationCollection.Name)]
public class MediaAssetConcurrencyTests : IAsyncLifetime
{
    private readonly MediaServiceFactory _factory;

    public MediaAssetConcurrencyTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task LosingWriter_ThrowsDbUpdateConcurrencyException()
    {
        var ownerId = Guid.NewGuid();
        var assetId = await TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);

        // Scope A: load the asset and capture its current xmin token in EF's change tracker.
        await using var scopeA = _factory.Services.CreateAsyncScope();
        var ctxA = scopeA.ServiceProvider.GetRequiredService<MediaDbContext>();
        var assetA = await ctxA.Assets.SingleAsync(a => a.Id == assetId);

        // Scope B updates the row out-of-band via ExecuteUpdate, which advances xmin in PG
        // without going through the change tracker. This simulates another pod / request
        // committing first.
        await using (var scopeB = _factory.Services.CreateAsyncScope())
        {
            var ctxB = scopeB.ServiceProvider.GetRequiredService<MediaDbContext>();
            await ctxB.Assets.Where(a => a.Id == assetId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.MimeType, "image/jpeg"));
        }

        // Scope A still holds the stale xmin -- attempting to soft-delete now must trip the
        // concurrency check on UPDATE ... WHERE xmin = <stale>, returning zero affected rows.
        assetA.SoftDelete();
        var act = async () => await ctxA.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "the xmin concurrency token must reject a writer whose snapshot of the row is stale");
    }
}
