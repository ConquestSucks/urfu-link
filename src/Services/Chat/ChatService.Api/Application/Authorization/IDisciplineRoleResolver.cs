using Urfu.Link.Services.Chat.Domain.Aggregates;

namespace Urfu.Link.Services.Chat.Application.Authorization;

/// <summary>
/// Resolves discipline-related authorization decisions. The default implementation is a
/// stub for #211 that allows any participant to pin in <c>Direct</c> conversations and
/// blocks pinning in <c>Group</c> conversations until #214 wires real role resolution.
/// </summary>
public interface IDisciplineRoleResolver
{
    Task<bool> CanPinAsync(Guid userId, Conversation conversation, CancellationToken cancellationToken);
}
