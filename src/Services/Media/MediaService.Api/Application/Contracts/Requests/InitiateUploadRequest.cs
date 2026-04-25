using MediaService.Api.Domain.Enums;

namespace MediaService.Api.Application.Contracts.Requests;

public sealed record InitiateUploadRequest(
    string FileName,
    long Size,
    string MimeType,
    Visibility Visibility);
