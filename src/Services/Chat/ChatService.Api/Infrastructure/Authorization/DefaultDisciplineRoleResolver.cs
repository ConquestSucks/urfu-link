using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Domain.Aggregates;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Infrastructure.Authorization;

/// <summary>
/// Stub used until <c>DisciplineService</c> (#207) and discipline chats (#214) are wired up.
/// Direct conversations: any participant can pin. Group conversations: blocked until the real
/// teacher/student role resolver replaces this implementation.
/// </summary>
internal sealed class DefaultDisciplineRoleResolver : IDisciplineRoleResolver
{
    public Task<bool> CanPinAsync(Guid userId, Conversation conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        var allowed = conversation.Type == ConversationType.Direct && conversation.IsParticipant(userId);
        return Task.FromResult(allowed);
    }
}
