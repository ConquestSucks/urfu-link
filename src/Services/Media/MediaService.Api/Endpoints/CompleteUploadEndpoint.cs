using FastEndpoints;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Urfu.Link.BuildingBlocks.Idempotency;

namespace MediaService.Api.Endpoints;

[Authorize]
public sealed class CompleteUploadEndpoint(
    IMediaAssetRepository assetRepository,
    IUploadSessionRepository sessionRepository,
    IMediaObjectStorage objectStorage)
    : Endpoint<CompleteUploadRequest>
{
    public override void Configure()
    {
        Post("/media/upload/complete");
        Options(x => x.AddEndpointFilter<IdempotencyEndpointFilter>());
        Summary(s => s.Summary = "Confirm the client uploaded bytes; transition the asset to Uploaded.");
    }

    public override async Task HandleAsync(CompleteUploadRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        var asset = await assetRepository.GetByIdAsync(req.AssetId, ct).ConfigureAwait(false);
        if (asset is null)
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

        var session = await sessionRepository.GetByAssetIdAsync(req.AssetId, ct).ConfigureAwait(false);
        if (session is null || session.IsCompleted || session.IsExpired(DateTimeOffset.UtcNow))
        {
            AddError("Upload session is missing or expired.");
            await HttpContext.Response.SendErrorsAsync(ValidationFailures, cancellation: ct).ConfigureAwait(false);
            return;
        }

        // Verify the object actually exists in MinIO.
        var meta = await objectStorage.GetMetadataAsync(asset.Bucket, asset.ObjectKey, ct).ConfigureAwait(false);
        if (meta is null)
        {
            AddError("Object was not found in storage. Did the client perform the PUT?");
            await HttpContext.Response.SendErrorsAsync(ValidationFailures, cancellation: ct).ConfigureAwait(false);
            return;
        }

        if (meta.ContentLength != asset.Size)
        {
            AddError($"Uploaded object size {meta.ContentLength} does not match declared size {asset.Size}.");
            await HttpContext.Response.SendErrorsAsync(ValidationFailures, cancellation: ct).ConfigureAwait(false);
            return;
        }

        asset.MarkUploaded(req.Checksum);
        session.MarkCompleted();

        await assetRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        await sessionRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        await HttpContext.Response.SendNoContentAsync(ct).ConfigureAwait(false);
    }
}
