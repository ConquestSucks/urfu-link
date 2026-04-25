namespace MediaService.Api.Application.Limits;

/// <summary>
/// Single source of truth for the small numeric and string limits that
/// constrain the public API surface (filename / mime-type / pagination).
/// File-size limits per <see cref="MediaService.Api.Domain.Enums.AssetKind"/>
/// live in <see cref="MediaLimitsOptions"/> because they are configurable
/// per environment; the values here are policy and stay in code.
/// </summary>
public static class MediaConstraints
{
    /// <summary>Upper bound on the sanitised object-key suffix (RFC-friendly).</summary>
    public const int MaxFileNameLength = 200;

    /// <summary>Fallback when sanitisation strips a filename to empty.</summary>
    public const string FileNameFallback = "file";

    /// <summary>RFC 4288 caps a registered media type at 127 characters.</summary>
    public const int MaxMimeTypeLength = 127;

    /// <summary>Default page size for GET /media/my when the caller omits ?limit=.</summary>
    public const int DefaultListLimit = 20;

    /// <summary>Hard cap on GET /media/my page size; larger requested values are clamped.</summary>
    public const int MaxListLimit = 100;
}
