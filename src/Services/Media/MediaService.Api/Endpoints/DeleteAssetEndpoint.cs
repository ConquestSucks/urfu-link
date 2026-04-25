using FastEndpoints;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Auth;

namespace MediaService.Api.Endpoints;

public sealed class DeleteAssetRequest
{
    public Guid AssetId { get; set; }
}

public sealed class DeleteAssetEndpoint(IMediaAssetRepository assetRepository)
    : Endpoint<DeleteAssetRequest>
{
    public override void Configure()
    {
        Delete("/media/{assetId}");
        Summary(s => s.Summary = "Soft-delete the asset. The MinIO object stays for the retention period.");
    }

    public override async Task HandleAsync(DeleteAssetRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var asset = await assetRepository.GetByIdAsync(req.AssetId, ct).ConfigureAwait(false);
        if (asset is null || !asset.IsAccessible)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        var userId = HttpContext.User.GetUserId();
        if (asset.OwnerId != userId)
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        asset.SoftDelete();
        await assetRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
