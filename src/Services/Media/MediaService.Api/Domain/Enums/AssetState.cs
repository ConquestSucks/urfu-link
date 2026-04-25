namespace MediaService.Api.Domain.Enums;

/// <summary>
/// Lifecycle state of a media asset:
/// Initiated (upload session created) → Uploaded (PUT confirmed via /complete)
/// → Deleted (soft delete) → HardDeleted (retention worker removed object).
/// Failed is a terminal state for upload sessions that did not complete in time.
/// </summary>
public enum AssetState
{
    Initiated = 0,
    Uploaded = 1,
    Deleted = 2,
    HardDeleted = 3,
    Failed = 4,
}
