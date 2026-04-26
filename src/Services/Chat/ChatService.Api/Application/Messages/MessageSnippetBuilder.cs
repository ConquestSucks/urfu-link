using System.Text;

namespace Urfu.Link.Services.Chat.Application.Messages;

/// <summary>
/// Builds a short highlighted snippet around the first occurrence of the search term in a
/// message body. Best-effort — returns null when the term cannot be located literally (e.g.
/// the stemmer matched a different morphological form).
/// </summary>
internal static class MessageSnippetBuilder
{
    private const int ContextChars = 30;

    public static string? Build(string body, string query)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var firstTerm = ExtractFirstTerm(query);
        if (firstTerm is null)
        {
            return null;
        }

        var matchIndex = body.IndexOf(firstTerm, StringComparison.OrdinalIgnoreCase);
        if (matchIndex < 0)
        {
            return null;
        }

        var start = Math.Max(0, matchIndex - ContextChars);
        var end = Math.Min(body.Length, matchIndex + firstTerm.Length + ContextChars);

        var sb = new StringBuilder();
        if (start > 0)
        {
            sb.Append("...");
        }
        sb.Append(body, start, end - start);
        if (end < body.Length)
        {
            sb.Append("...");
        }
        return sb.ToString();
    }

    private static string? ExtractFirstTerm(string query)
    {
        var sb = new StringBuilder();
        foreach (var ch in query)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0)
            {
                break;
            }
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }
}
