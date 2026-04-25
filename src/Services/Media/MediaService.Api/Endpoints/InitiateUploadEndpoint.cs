using FastEndpoints;
using MediaService.Api.Application.Contracts.Requests;
using MediaService.Api.Application.Contracts.Responses;
using MediaService.Api.Application.Limits;
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

        if (!MimeTypeCatalog.TryResolve(req.MimeType, out var kind))
        {
            AddError(r => r.MimeType, "Mime type is not in the white-list.");
            await HttpContext.Response.SendErrorsAsync(ValidationFailures, cancellation: ct).ConfigureAwait(false);
            return;
        }

        var ownerId = HttpContext.User.GetUserId();
        var bucket = req.Visibility == Visibility.Public
            ? storageOptions.Value.PublicBucket
            : storageOptions.Value.PrivateBucket;

        var assetId = Guid.NewGuid();
        var objectKey = $"{ownerId:N}/{assetId:N}/{SanitizeFileName(req.FileName)}";

        var asset = MediaAsset.Initiate(
            assetId, ownerId, req.Visibility, kind, bucket, objectKey, req.Size, req.MimeType, req.FileName);
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

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string([.. fileName.Where(c => !invalid.Contains(c))]);
        return string.IsNullOrEmpty(sanitized) ? "file" : sanitized;
    }
}
