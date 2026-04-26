using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Infrastructure.Authorization;

/// <summary>
/// Real role resolution for discipline-driven conversations:
/// - Direct conversations: any participant can pin (parity with the previous behavior).
/// - Group conversations: only participants flagged as <see cref="ParticipantRole.Teacher"/>
///   can pin / manage messages. The role information arrives via the
///   <c>urfu.discipline.events.v1</c> topic and is mirrored into <see cref="Conversation.ParticipantRoles"/>.
/// </summary>
internal sealed class DefaultDisciplineRoleResolver : IDisciplineRoleResolver
{
    public Task<bool> CanPinAsync(Guid userId, Conversation conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        _ = cancellationToken;

        if (!conversation.IsParticipant(userId))
        {
            return Task.FromResult(false);
        }

        var allowed = conversation.Type == ConversationType.Direct || conversation.IsTeacher(userId);
        return Task.FromResult(allowed);
    }
}
