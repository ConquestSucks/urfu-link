using FastEndpoints;
using MediaService.Api.Application.Access;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Auth;

namespace MediaService.Api.Endpoints;

public sealed class GetMetadataRequest
{
    public Guid AssetId { get; set; }
}

public sealed class GetMetadataEndpoint(
    IMediaAssetRepository assetRepository,
    AccessPolicy accessPolicy)
    : Endpoint<GetMetadataRequest, AssetMetadataResponse>
{
    public override void Configure()
    {
        Get("{assetId}/metadata");
        Group<MediaGroup>();
        Summary(s => s.Summary = "Return asset metadata if the user has access (no URL).");
    }

    public override async Task HandleAsync(GetMetadataRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var asset = await assetRepository.GetByIdAsync(req.AssetId, ct).ConfigureAwait(false);
        if (asset is null || !asset.IsAccessible)
        {
            await Send.NotFoundAsync(ct).ConfigureAwait(false);
            return;
        }

        var userId = HttpContext.User.GetUserId();
        if (!await accessPolicy.CanDownloadAsync(asset, userId, ct).ConfigureAwait(false))
        {
            await Send.ForbiddenAsync(ct).ConfigureAwait(false);
            return;
        }

        await HttpContext.Response.SendAsync(new AssetMetadataResponse(
            asset.Id,
            asset.OwnerId,
            asset.Visibility,
            asset.Kind,
            asset.Size,
            asset.MimeType,
            asset.OriginalFileName,
            asset.State,
            asset.CreatedAtUtc,
            asset.UploadedAtUtc,
            asset.DurationSeconds), cancellation: ct).ConfigureAwait(false);
    }
}
