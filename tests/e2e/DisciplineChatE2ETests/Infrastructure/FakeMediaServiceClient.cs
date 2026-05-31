using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace DisciplineChatE2ETests.Infrastructure;

public sealed class FakeMediaServiceClient : IMediaServiceClient
{
    public Task<IReadOnlyList<MediaAssetMetadata>> BatchGetMetadataAsync(
        IReadOnlyList<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<IReadOnlyList<MediaAssetMetadata>>([]);
    }

    public Task GrantConversationAccessAsync(
        Guid assetId,
        IReadOnlyList<Guid> userIds,
        string conversationId,
        Guid grantedByUserId,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
