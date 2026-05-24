using MediaService.Api.Domain.Enums;

namespace MediaService.Api.Application.Limits;

/// <summary>
/// MIME type → <see cref="AssetKind"/> resolver with explicit white-listing.
/// Anything not in the catalog is rejected at /upload/init.
/// Executable / scripting MIME types are intentionally excluded.
/// </summary>
public static class MimeTypeCatalog
{
    private static readonly Dictionary<string, AssetKind> Map =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Image
            ["image/jpeg"] = AssetKind.Image,
            ["image/png"] = AssetKind.Image,
            ["image/webp"] = AssetKind.Image,
            ["image/gif"] = AssetKind.Image,
            ["image/heic"] = AssetKind.Image,
            ["image/heif"] = AssetKind.Image,

            // Video
            ["video/mp4"] = AssetKind.Video,
            ["video/quicktime"] = AssetKind.Video,
            ["video/webm"] = AssetKind.Video,

            // Voice (short, recorded in app)
            ["audio/ogg"] = AssetKind.Voice,
            ["audio/opus"] = AssetKind.Voice,

            // Audio (general music / longer audio)
            ["audio/mpeg"] = AssetKind.Audio,
            ["audio/mp4"] = AssetKind.Audio,
            ["audio/m4a"] = AssetKind.Audio,
            ["audio/x-m4a"] = AssetKind.Audio,

            // Documents
            ["application/pdf"] = AssetKind.Document,
            ["application/msword"] = AssetKind.Document,
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = AssetKind.Document,
            ["application/vnd.ms-excel"] = AssetKind.Document,
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = AssetKind.Document,
            ["application/vnd.ms-powerpoint"] = AssetKind.Document,
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = AssetKind.Document,
            ["application/json"] = AssetKind.Document,
            ["text/plain"] = AssetKind.Document,
            ["text/csv"] = AssetKind.Document,
            ["text/markdown"] = AssetKind.Document,
            ["application/zip"] = AssetKind.Document,
            ["application/x-zip-compressed"] = AssetKind.Document,
        };

    public static bool TryResolve(string mimeType, out AssetKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        return Map.TryGetValue(mimeType.Trim(), out kind);
    }
}
