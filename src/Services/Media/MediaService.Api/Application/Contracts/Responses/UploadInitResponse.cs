namespace MediaService.Api.Application.Contracts.Responses;

public sealed record UploadInitResponse(
    Guid AssetId,
    string PresignedPutUrl,
    DateTimeOffset ExpiresAtUtc,
    string Bucket,
    string ObjectKey);
