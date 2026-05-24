using FastEndpoints;
using MediaService.Api.Application.Access;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Auth;
using MediaService.Api.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace MediaService.Api.Endpoints;

public sealed class GetDownloadUrlEndpoint(
    IMediaAssetRepository assetRepository,
    IPresignedUrlGenerator urlGenerator,
    AccessPolicy accessPolicy,
    IOptions<StorageOptions> storageOptions)
    : EndpointWithoutRequest<DownloadUrlResponse>
{
    public override void Configure()
    {
        Get("{assetId}/download-url");
        Group<MediaGroup>();
        Summary(s => s.Summary = "Issue a short-lived presigned GET URL if the user has access.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var assetId = Route<Guid>("assetId");

        var asset = await assetRepository.GetByIdAsync(assetId, ct).ConfigureAwait(false);
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

        var presigned = urlGenerator.ForDownload(
            asset.Bucket,
            asset.ObjectKey,
            asset.OriginalFileName,
            storageOptions.Value.DownloadUrlTtl);
        await HttpContext.Response.SendAsync(
            new DownloadUrlResponse(presigned.Url, presigned.ExpiresAtUtc),
            cancellation: ct).ConfigureAwait(false);
    }
}
