namespace MediaService.Api.Domain.Enums;

/// <summary>
/// High-level category of the asset. Derived from MIME type at upload init
/// time and used to apply per-category size/duration limits.
/// </summary>
public enum AssetKind
{
    Image = 0,
    Video = 1,
    Audio = 2,
    Voice = 3,
    Document = 4,
}
