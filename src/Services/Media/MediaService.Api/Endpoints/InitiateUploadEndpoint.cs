using FastEndpoints;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Application.Limits;
using MediaService.Api.Application.Storage;
using MediaService.Api.Domain;
using MediaService.Api.Domain.Enums;
using MediaService.Api.Domain.Interfaces;
using MediaService.Api.Infrastructure.Auth;
using MediaService.Api.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Urfu.Link.BuildingBlocks.Idempotency;

namespace MediaService.Api.Endpoints;

public sealed class InitiateUploadEndpoint(
    IMediaAssetRepository assetRepository,
    IUploadSessionRepository sessionRepository,
    IPresignedUrlGenerator urlGenerator,
    IOptions<StorageOptions> storageOptions)
    : Endpoint<InitiateUploadRequest, UploadInitResponse>
{
    public override void Configure()
    {
        Post("upload/init");
        Group<MediaGroup>();
        Options(x => x.AddEndpointFilter<IdempotencyEndpointFilter>());
        Summary(s => s.Summary = "Reserve a media asset record and return a presigned PUT URL.");
    }

    public override async Task HandleAsync(InitiateUploadRequest req, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(req);

        // InitiateUploadValidator has already accepted the MIME type — TryResolve cannot fail.
        MimeTypeCatalog.TryResolve(req.MimeType, req.RequestedKind, out var kind);

        var ownerId = HttpContext.User.GetUserId();
        var bucket = req.Visibility == Visibility.Public
            ? storageOptions.Value.PublicBucket
            : storageOptions.Value.PrivateBucket;

        var assetId = Guid.NewGuid();
        var objectKey = $"{ownerId:N}/{assetId:N}/{FileNameSanitizer.Sanitize(req.FileName)}";

        var asset = MediaAsset.Initiate(
            assetId,
            ownerId,
            req.Visibility,
            kind,
            bucket,
            objectKey,
            req.Size,
            req.MimeType,
            req.FileName,
            kind == AssetKind.Voice ? req.DurationSeconds : null);
        assetRepository.Add(asset);

        var session = UploadSession.Open(assetId, storageOptions.Value.UploadSessionTtl);
        sessionRepository.Add(session);

        await assetRepository.SaveChangesAsync(ct).ConfigureAwait(false);
        await sessionRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        var presigned = urlGenerator.ForUpload(bucket, objectKey, req.MimeType, storageOptions.Value.UploadUrlTtl);

        await HttpContext.Response.SendAsync(
            new UploadInitResponse(assetId, presigned.Url, presigned.ExpiresAtUtc, bucket, objectKey),
            cancellation: ct).ConfigureAwait(false);
    }

}
