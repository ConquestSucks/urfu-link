using System.Text.RegularExpressions;

namespace MediaService.Api.Application.Storage;

/// <summary>
/// Reduces a user-supplied file name to a safe object-key suffix using an
/// allowlist of ASCII letters, digits, dot, underscore and hyphen. Anything
/// else (path separators, RTL overrides, control characters, non-ASCII
/// codepoints) is dropped before truncating to <see cref="MaxLength"/>.
/// Defense-in-depth even though the final S3 object key has a Guid prefix.
/// </summary>
public static partial class FileNameSanitizer
{
    public const int MaxLength = 200;
    public const string Fallback = "file";

    [GeneratedRegex("[^A-Za-z0-9._-]+", RegexOptions.CultureInvariant)]
    private static partial Regex Disallowed();

    [GeneratedRegex("^[^A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingNonAlphaNumeric();

    public static string Sanitize(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return Fallback;

        var stripped = Disallowed().Replace(fileName, string.Empty);
        stripped = LeadingNonAlphaNumeric().Replace(stripped, string.Empty);
        if (stripped.Length == 0) return Fallback;

        return stripped.Length > MaxLength ? stripped[..MaxLength] : stripped;
    }
}
