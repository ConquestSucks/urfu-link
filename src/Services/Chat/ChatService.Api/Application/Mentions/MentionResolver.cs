using System.Text.RegularExpressions;
using Urfu.Link.Services.Chat.Application.Disciplines;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Application.Mentions;

/// <summary>
/// Resolves the full mention set for a message body, including the discipline-only
/// <c>@teachers</c> and <c>@students</c> tokens that <see cref="MentionsParser"/>
/// alone cannot expand. Composes with <see cref="MentionsParser.Parse"/> for the
/// participant-only tokens (<c>@&lt;guid&gt;</c>, <c>@everyone</c>) and adds an async
/// roundtrip to <see cref="IDisciplineServiceClient.ListMembersAsync"/> when the
/// conversation is a discipline group.
/// </summary>
/// <remarks>
/// Direct conversations reject <c>@teachers</c> / <c>@students</c> by silently dropping
/// the token — there is no role hierarchy in a 1:1 chat. The discipline gRPC call is
/// uncached at this layer; PresenceService-style projection caching can be layered
/// on later if the round-trip becomes a hot path.
/// </remarks>
public sealed partial class MentionResolver(IDisciplineServiceClient disciplineServiceClient)
{
    [GeneratedRegex(@"@(?<token>teachers|students)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex SpecialTokensRegex();

    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        string? body,
        Conversation conversation,
        int maxMentions,
        CancellationToken cancellationToken,
        IReadOnlyList<Guid>? explicitMentionUserIds = null)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxMentions);

        // Step 1: explicit user ids and @everyone — purely synchronous, no gRPC.
        var mentions = MentionsParser.Parse(body, conversation.Participants, maxMentions).ToList();
        AddExplicitMentionIds(
            mentions,
            explicitMentionUserIds ?? Array.Empty<Guid>(),
            conversation.Participants,
            maxMentions);

        if (mentions.Count >= maxMentions || string.IsNullOrEmpty(body))
        {
            return mentions;
        }

        // Step 2: @teachers / @students — discipline-only, requires gRPC.
        if (conversation.GroupSubtype != GroupSubtype.Discipline ||
            conversation.DisciplineId is not Guid disciplineId)
        {
            return mentions;
        }

        var (mentionsTeachers, mentionsStudents) = ScanSpecialTokens(body);
        if (!mentionsTeachers && !mentionsStudents)
        {
            return mentions;
        }

        var members = await disciplineServiceClient
            .ListMembersAsync(disciplineId, cancellationToken)
            .ConfigureAwait(false);

        var participantSet = new HashSet<Guid>(conversation.Participants);
        var seen = new HashSet<Guid>(mentions);

        foreach (var member in members)
        {
            if (mentions.Count >= maxMentions)
            {
                break;
            }

            // Skip if not actually in the conversation (the discipline roster and the chat
            // roster can drift between an enrollment Kafka event and its projection).
            if (!participantSet.Contains(member.UserId))
            {
                continue;
            }

            var roleMatches = (mentionsTeachers && member.Role == ParticipantRole.Teacher)
                           || (mentionsStudents && member.Role == ParticipantRole.Student);

            if (roleMatches && seen.Add(member.UserId))
            {
                mentions.Add(member.UserId);
            }
        }

        return mentions;
    }

    private static void AddExplicitMentionIds(
        List<Guid> mentions,
        IReadOnlyList<Guid> explicitMentionUserIds,
        IReadOnlyList<Guid> participants,
        int maxMentions)
    {
        if (explicitMentionUserIds.Count == 0 || mentions.Count >= maxMentions)
        {
            return;
        }

        var participantSet = new HashSet<Guid>(participants);
        var seen = new HashSet<Guid>(mentions);
        foreach (var userId in explicitMentionUserIds)
        {
            if (mentions.Count >= maxMentions)
            {
                return;
            }

            if (participantSet.Contains(userId) && seen.Add(userId))
            {
                mentions.Add(userId);
            }
        }
    }

    /// <summary>Walks the body once to detect whether teachers/students tokens are present.</summary>
    private static (bool teachers, bool students) ScanSpecialTokens(string body)
    {
        var teachers = false;
        var students = false;
        foreach (Match match in SpecialTokensRegex().Matches(body))
        {
            var token = match.Groups["token"].Value;
            if (string.Equals(token, "teachers", StringComparison.OrdinalIgnoreCase))
            {
                teachers = true;
            }
            else if (string.Equals(token, "students", StringComparison.OrdinalIgnoreCase))
            {
                students = true;
            }
        }

        return (teachers, students);
    }
}
