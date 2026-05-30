using MediaService.Api.Domain.Enums;

namespace MediaService.Api.Application.Contracts.Requests;

/// <summary>
/// Asset metadata supplied by the recording client. <see cref="DurationSeconds"/> is
/// declared by the client (the recorder UI knows the recording length); for voice it
/// is mandatory and is enforced against <c>MediaLimits:Voice:MaxDurationSeconds</c>.
/// Other kinds may set it to null.
/// </summary>
public sealed record InitiateUploadRequest(
    string FileName,
    long Size,
    string MimeType,
    Visibility Visibility,
    AssetKind? RequestedKind = null,
    int? DurationSeconds = null);
