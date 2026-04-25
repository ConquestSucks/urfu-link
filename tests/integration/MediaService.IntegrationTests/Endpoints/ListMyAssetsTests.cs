using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Infrastructure.Persistence;
using MediaService.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaService.IntegrationTests.Endpoints;

[Collection(IntegrationCollection.Name)]
public class ListMyAssetsTests : IAsyncLifetime
{
    private readonly MediaServiceFactory _factory;

    public ListMyAssetsTests(MediaServiceFactory factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => _factory.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ReturnsOnlyOwnerUploadedAssets()
    {
        var ownerId = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        await CreateUploadedAssetAsync(ownerId);
        await CreateUploadedAssetAsync(ownerId);
        await CreateUploadedAssetAsync(stranger);

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.GetAsync("/api/v1/media/my");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
        body!.Items.Should().HaveCount(2);
        body.Items.Should().OnlyContain(i => i.OwnerId == ownerId);
        body.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ExcludesNonUploadedAssets()
    {
        var ownerId = Guid.NewGuid();
        await TestAssetBuilder.InitAsync(_factory, ownerId); // Initiated, not Uploaded
        await CreateUploadedAssetAsync(ownerId);

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var response = await client.GetAsync("/api/v1/media/my");

        var body = await response.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
        body!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task PaginationCursorWalksFullList()
    {
        var ownerId = Guid.NewGuid();
        var assetIds = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            assetIds.Add(await CreateUploadedAssetAsync(ownerId));
        }

        var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var firstPage = await client.GetAsync("/api/v1/media/my?limit=2");
        var first = await firstPage.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
        first!.Items.Should().HaveCount(2);
        first.NextCursor.Should().NotBeNull();

        var secondClient = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
        var secondPage = await secondClient.GetAsync($"/api/v1/media/my?limit=2&cursor={first.NextCursor}");
        var second = await secondPage.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
        second!.Items.Should().HaveCount(2);

        var seen = first.Items.Concat(second.Items).Select(i => i.AssetId).ToList();
        seen.Distinct().Should().HaveCount(4, "pages must not overlap");
    }

    [Fact]
    public async Task PaginationCursor_HandlesTiesInCreatedAtUtc()
    {
        var ownerId = Guid.NewGuid();
        var assetIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            assetIds.Add(await CreateUploadedAssetAsync(ownerId));
        }

        // Force all three to share the exact same CreatedAtUtc to exercise the
        // keyset tie-breaker. PostgreSQL has microsecond precision, so this
        // collision is rare in practice but legal -- and the previous strict
        // `CreatedAtUtc < anchor` filter dropped tied rows from every page.
        using (var scope = _factory.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<MediaDbContext>();
            var fixedTime = DateTimeOffset.UtcNow;
            await ctx.Assets.Where(a => assetIds.Contains(a.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedAtUtc, fixedTime));
        }

        var seen = new List<Guid>();
        Guid? cursor = null;
        for (var page = 0; page < 5; page++)
        {
            var client = TestAssetBuilder.AuthorizedClient(_factory, ownerId);
            var url = cursor.HasValue
                ? $"/api/v1/media/my?limit=1&cursor={cursor}"
                : "/api/v1/media/my?limit=1";
            var resp = await client.GetAsync(url);
            var body = await resp.Content.ReadFromJsonAsync<ListMyAssetsResponse>();
            seen.AddRange(body!.Items.Select(i => i.AssetId));
            cursor = body.NextCursor;
            if (cursor is null) break;
        }

        seen.Distinct().Should().HaveCount(3,
            "all assets must be visible across pages even when their CreatedAtUtc collides");
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        TestAuthHandler.CurrentPrincipal = null;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/media/my");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task<Guid> CreateUploadedAssetAsync(Guid ownerId)
        => TestAssetBuilder.CreateUploadedAssetAsync(_factory, ownerId);
}
