namespace MediaService.Api.Application.Contracts.Requests;

public sealed record CompleteUploadRequest(Guid AssetId, string? Checksum);
