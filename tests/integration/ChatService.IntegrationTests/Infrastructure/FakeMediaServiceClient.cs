using System.Collections.Concurrent;
using Urfu.Link.Services.Chat.Application.Messages;

namespace ChatService.IntegrationTests.Infrastructure;

public sealed record GrantAccessCall(Guid AssetId, IReadOnlyList<Guid> UserIds, string ConversationId, Guid GrantedByUserId);

public sealed class FakeMediaServiceClient : IMediaServiceClient
{
    private readonly ConcurrentDictionary<(Guid AssetId, Guid UserId), bool> _ownership = new();

    public ConcurrentBag<GrantAccessCall> Grants { get; } = [];

    public bool DefaultOwnership { get; set; } = true;

    public void SetOwnership(Guid assetId, Guid userId, bool isOwner)
        => _ownership[(assetId, userId)] = isOwner;

    public Task<bool> CheckOwnershipAsync(Guid assetId, Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_ownership.TryGetValue((assetId, userId), out var owns) ? owns : DefaultOwnership);

    public Task GrantConversationAccessAsync(
        Guid assetId,
        IReadOnlyList<Guid> userIds,
        string conversationId,
        Guid grantedByUserId,
        CancellationToken cancellationToken)
    {
        Grants.Add(new GrantAccessCall(assetId, userIds.ToList(), conversationId, grantedByUserId));
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _ownership.Clear();
        Grants.Clear();
        DefaultOwnership = true;
    }
}
