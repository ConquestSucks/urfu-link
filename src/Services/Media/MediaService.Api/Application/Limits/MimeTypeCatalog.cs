using System.Net.Http.Headers;
using MediaService.Api.Domain.Enums;

namespace MediaService.Api.Application.Limits;

/// <summary>
/// MIME type → <see cref="AssetKind"/> resolver with explicit white-listing.
/// Anything not in the catalog is rejected at /upload/init.
/// Executable / scripting MIME types are intentionally excluded.
/// </summary>
public static class MimeTypeCatalog
{
    private static readonly HashSet<string> VoiceRecorderMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "audio/m4a",
            "audio/mp4",
            "audio/x-m4a",
            "audio/webm",
            "audio/ogg",
            "audio/opus",
        };

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
            ["image/bmp"] = AssetKind.Image,
            ["image/tiff"] = AssetKind.Image,

            // Video
            ["video/mp4"] = AssetKind.Video,
            ["video/quicktime"] = AssetKind.Video,
            ["video/webm"] = AssetKind.Video,
            ["video/mpeg"] = AssetKind.Video,
            ["video/x-msvideo"] = AssetKind.Video,
            ["video/x-matroska"] = AssetKind.Video,

            // Voice (short, recorded in app)
            ["audio/ogg"] = AssetKind.Voice,
            ["audio/opus"] = AssetKind.Voice,

            // Audio (general music / longer audio)
            ["audio/mpeg"] = AssetKind.Audio,
            ["audio/mp4"] = AssetKind.Audio,
            ["audio/m4a"] = AssetKind.Audio,
            ["audio/x-m4a"] = AssetKind.Audio,
            ["audio/webm"] = AssetKind.Audio,
            ["audio/wav"] = AssetKind.Audio,
            ["audio/x-wav"] = AssetKind.Audio,
            ["audio/aac"] = AssetKind.Audio,
            ["audio/flac"] = AssetKind.Audio,

            // Documents
            ["application/pdf"] = AssetKind.Document,
            ["application/msword"] = AssetKind.Document,
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = AssetKind.Document,
            ["application/vnd.ms-excel"] = AssetKind.Document,
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] = AssetKind.Document,
            ["application/vnd.ms-powerpoint"] = AssetKind.Document,
            ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = AssetKind.Document,
            ["application/vnd.oasis.opendocument.text"] = AssetKind.Document,
            ["application/vnd.oasis.opendocument.spreadsheet"] = AssetKind.Document,
            ["application/vnd.oasis.opendocument.presentation"] = AssetKind.Document,
            ["application/rtf"] = AssetKind.Document,
            ["text/rtf"] = AssetKind.Document,
            ["application/json"] = AssetKind.Document,
            ["application/x-ndjson"] = AssetKind.Document,
            ["application/xml"] = AssetKind.Document,
            ["text/xml"] = AssetKind.Document,
            ["application/yaml"] = AssetKind.Document,
            ["application/x-yaml"] = AssetKind.Document,
            ["text/yaml"] = AssetKind.Document,
            ["text/x-yaml"] = AssetKind.Document,
            ["application/toml"] = AssetKind.Document,
            ["text/x-toml"] = AssetKind.Document,
            ["text/plain"] = AssetKind.Document,
            ["text/csv"] = AssetKind.Document,
            ["text/markdown"] = AssetKind.Document,
            ["text/javascript"] = AssetKind.Document,
            ["application/javascript"] = AssetKind.Document,
            ["application/typescript"] = AssetKind.Document,
            ["text/x-python"] = AssetKind.Document,
            ["text/x-csharp"] = AssetKind.Document,
            ["text/x-java-source"] = AssetKind.Document,
            ["text/x-kotlin"] = AssetKind.Document,
            ["text/x-go"] = AssetKind.Document,
            ["text/x-rust"] = AssetKind.Document,
            ["application/zip"] = AssetKind.Document,
            ["application/x-zip-compressed"] = AssetKind.Document,
            ["application/x-7z-compressed"] = AssetKind.Document,
            ["application/vnd.rar"] = AssetKind.Document,
            ["application/x-rar-compressed"] = AssetKind.Document,
            ["application/x-tar"] = AssetKind.Document,
            ["application/gzip"] = AssetKind.Document,
            ["application/x-gzip"] = AssetKind.Document,
        };

    public static bool TryResolve(string mimeType, out AssetKind kind)
        => TryResolve(mimeType, requestedKind: null, out kind);

    public static bool TryResolve(string mimeType, AssetKind? requestedKind, out AssetKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        var normalized = NormalizeMimeType(mimeType);

        if (requestedKind is null)
        {
            return Map.TryGetValue(normalized, out kind);
        }

        if (requestedKind == AssetKind.Voice && VoiceRecorderMimeTypes.Contains(normalized))
        {
            kind = AssetKind.Voice;
            return true;
        }

        kind = default;
        return false;
    }

    public static bool IsSupportedRequestedKind(AssetKind requestedKind)
        => requestedKind == AssetKind.Voice;

    private static string NormalizeMimeType(string mimeType)
    {
        if (MediaTypeHeaderValue.TryParse(mimeType, out var parsed)
            && !string.IsNullOrWhiteSpace(parsed.MediaType))
        {
            return parsed.MediaType;
        }

        var parameterIndex = mimeType.IndexOf(';', StringComparison.Ordinal);
        return (parameterIndex >= 0 ? mimeType[..parameterIndex] : mimeType).Trim();
    }
}
