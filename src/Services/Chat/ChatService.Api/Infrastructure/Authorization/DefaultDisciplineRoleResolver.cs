using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Infrastructure.Authorization;

/// <summary>
/// Real role resolution for discipline-driven conversations:
/// - <c>callerIsAdmin = true</c> always wins: realm admins manage pins anywhere,
///   even in conversations they have no enrolment in.
/// - Direct conversations: any participant can pin (parity with the previous behaviour).
/// - Group conversations: only participants flagged as <see cref="ParticipantRole.Teacher"/>
///   can pin / manage messages. The role information arrives via the
///   <c>urfu.discipline.events.v1</c> topic and is mirrored into <see cref="Conversation.ParticipantRoles"/>.
/// </summary>
internal sealed class DefaultDisciplineRoleResolver : IDisciplineRoleResolver
{
    public Task<bool> CanPinAsync(
        Guid userId,
        bool callerIsAdmin,
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        _ = cancellationToken;

        if (callerIsAdmin)
        {
            return Task.FromResult(true);
        }

        if (!conversation.IsParticipant(userId))
        {
            return Task.FromResult(false);
        }

        var allowed = conversation.Type == ConversationType.Direct || conversation.IsTeacher(userId);
        return Task.FromResult(allowed);
    }
}
