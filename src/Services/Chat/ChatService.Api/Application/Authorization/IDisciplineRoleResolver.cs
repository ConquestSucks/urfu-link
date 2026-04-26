using Urfu.Link.Services.Chat.Domain.Aggregates;

namespace Urfu.Link.Services.Chat.Application.Authorization;

/// <summary>
/// Resolves discipline-related authorization decisions for chat operations.
/// </summary>
public interface IDisciplineRoleResolver
{
    /// <summary>
    /// Whether the caller is allowed to pin/unpin a message in <paramref name="conversation"/>.
    /// Direct conversations: any participant can pin. Group conversations sourced from a
    /// discipline: only participants whose role is Teacher. The <paramref name="callerIsAdmin"/>
    /// flag short-circuits the membership check — admins can manage pins on any
    /// conversation, even if they are not enrolled.
    /// </summary>
    Task<bool> CanPinAsync(
        Guid userId,
        bool callerIsAdmin,
        Conversation conversation,
        CancellationToken cancellationToken);
}
