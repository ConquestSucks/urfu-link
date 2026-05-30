using MediaService.Api.Domain.Enums;

namespace MediaService.Api.Application.Contracts.Responses;

public sealed record AssetMetadataResponse(
    Guid AssetId,
    Guid OwnerId,
    Visibility Visibility,
    AssetKind Kind,
    long Size,
    string MimeType,
    string OriginalFileName,
    AssetState State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UploadedAtUtc,
    int? DurationSeconds = null);
