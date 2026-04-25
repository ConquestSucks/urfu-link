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
    string State,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UploadedAtUtc);
