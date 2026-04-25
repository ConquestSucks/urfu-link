using System.Text.RegularExpressions;
using MediaService.Api.Application.Limits;

namespace MediaService.Api.Application.Storage;

/// <summary>
/// Reduces a user-supplied file name to a safe object-key suffix using an
/// allowlist of ASCII letters, digits, dot, underscore and hyphen. Anything
/// else (path separators, RTL overrides, control characters, non-ASCII
/// codepoints) is dropped before truncating to <see cref="MediaConstraints.MaxFileNameLength"/>.
/// Defense-in-depth even though the final S3 object key has a Guid prefix.
/// </summary>
public static partial class FileNameSanitizer
{
    [GeneratedRegex("[^A-Za-z0-9._-]+", RegexOptions.CultureInvariant)]
    private static partial Regex Disallowed();

    [GeneratedRegex("^[^A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNonAlphaNumeric();

    public static string Sanitize(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return MediaConstraints.FileNameFallback;

        var stripped = Disallowed().Replace(fileName, string.Empty);
        stripped = LeadingNonAlphaNumeric().Replace(stripped, string.Empty);
        if (stripped.Length == 0) return MediaConstraints.FileNameFallback;

        return stripped.Length > MediaConstraints.MaxFileNameLength
            ? stripped[..MediaConstraints.MaxFileNameLength]
            : stripped;
    }
}
