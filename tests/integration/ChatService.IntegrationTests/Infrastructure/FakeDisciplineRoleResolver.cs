using Urfu.Link.Services.Chat.Application.Authorization;
using Urfu.Link.Services.Chat.Domain.Aggregates;

namespace ChatService.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces the production <see cref="IDisciplineRoleResolver"/> stub during integration tests.
/// By default allows pinning everywhere so happy-path tests don't have to wire authz; tests
/// that exercise authz failure flip <see cref="Predicate"/> to a stricter check (e.g.
/// <c>(_, _, c) =&gt; c.Type == ConversationType.Direct</c>).
/// </summary>
public sealed class FakeDisciplineRoleResolver : IDisciplineRoleResolver
{
    public Func<Guid, bool, Conversation, bool> Predicate { get; set; } = (_, _, _) => true;

    public Task<bool> CanPinAsync(
        Guid userId,
        bool callerIsAdmin,
        Conversation conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return Task.FromResult(Predicate(userId, callerIsAdmin, conversation));
    }

    public void Reset()
    {
        Predicate = (_, _, _) => true;
    }
}
