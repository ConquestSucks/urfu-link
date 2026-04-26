using System.Text.RegularExpressions;

namespace Urfu.Link.Services.Chat.Application.Mentions;

/// <summary>
/// Parses <c>@</c> mentions from a message body. Supported forms:
/// <list type="bullet">
///   <item><c>@&lt;guid&gt;</c> — direct user mention.</item>
///   <item><c>@everyone</c> — expands to all participants (excluding the sender is a caller decision).</item>
///   <item><c>@teachers</c>, <c>@students</c> — discipline-only special tokens. Stubbed to empty
///     in #211 until <c>IDisciplineRoleResolver</c> learns about roles in #214.</item>
/// </list>
/// Unknown mentions and mentions of users that aren't participants are dropped silently.
/// Output is deduplicated and capped at <c>maxMentions</c>.
/// </summary>
public static partial class MentionsParser
{
    [GeneratedRegex(@"@(?<token>everyone|teachers|students|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex MentionRegex();

    public static IReadOnlyList<Guid> Parse(
        string? body,
        IReadOnlyList<Guid> participants,
        int maxMentions)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMentions);

        if (string.IsNullOrEmpty(body))
        {
            return Array.Empty<Guid>();
        }

        var participantSet = new HashSet<Guid>(participants);
        var ordered = new List<Guid>();
        var seen = new HashSet<Guid>();

        foreach (Match match in MentionRegex().Matches(body))
        {
            var token = match.Groups["token"].Value;
            switch (token)
            {
                case var t when string.Equals(t, "everyone", StringComparison.OrdinalIgnoreCase):
                    foreach (var participant in participants)
                    {
                        if (seen.Add(participant))
                        {
                            ordered.Add(participant);
                            if (ordered.Count >= maxMentions)
                            {
                                return ordered;
                            }
                        }
                    }
                    break;
                case var t when string.Equals(t, "teachers", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(t, "students", StringComparison.OrdinalIgnoreCase):
                    // Stubbed in #211; #214 will resolve via IDisciplineRoleResolver.
                    break;
                default:
                    if (Guid.TryParseExact(token, "D", out var userId)
                        && participantSet.Contains(userId)
                        && seen.Add(userId))
                    {
                        ordered.Add(userId);
                        if (ordered.Count >= maxMentions)
                        {
                            return ordered;
                        }
                    }
                    break;
            }
        }

        return ordered;
    }
}
