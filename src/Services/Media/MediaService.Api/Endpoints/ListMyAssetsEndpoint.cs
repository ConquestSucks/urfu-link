using FastEndpoints;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Auth;

namespace MediaService.Api.Endpoints;

public sealed class ListMyAssetsRequest
{
    public Guid? Cursor { get; set; }
    public int Limit { get; set; } = DefaultLimit;
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
}

public sealed class ListMyAssetsEndpoint(IMediaAssetRepository assetRepository)
    : Endpoint<ListMyAssetsRequest, ListMyAssetsResponse>
{
    public override void Configure()
    {
        Get("my");
        Group<MediaGroup>();
        Summary(s => s.Summary = "Paginated list of the caller's uploaded assets, newest first.");
    }

    public override async Task HandleAsync(ListMyAssetsRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var limit = Math.Clamp(req.Limit, 1, ListMyAssetsRequest.MaxLimit);
        var ownerId = HttpContext.User.GetUserId();

        var page = await assetRepository.ListByOwnerAsync(ownerId, req.Cursor, limit, ct).ConfigureAwait(false);

        var items = page.Items
            .Select(a => new AssetMetadataResponse(
                a.Id, a.OwnerId, a.Visibility, a.Kind, a.Size,
                a.MimeType, a.OriginalFileName, a.State,
                a.CreatedAtUtc, a.UploadedAtUtc))
            .ToList();

        await HttpContext.Response.SendAsync(
            new ListMyAssetsResponse(items, page.NextCursor), cancellation: ct).ConfigureAwait(false);
    }
}
