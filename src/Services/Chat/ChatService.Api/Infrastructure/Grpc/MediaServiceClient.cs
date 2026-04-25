using System.Globalization;
using MediaService.Api.Grpc;
using Urfu.Link.Services.Chat.Application.Messages;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

internal sealed class MediaServiceClient(InternalApi.InternalApiClient grpcClient) : IMediaServiceClient
{
    public async Task<bool> CheckOwnershipAsync(Guid assetId, Guid userId, CancellationToken cancellationToken)
    {
        var reply = await grpcClient.CheckOwnershipAsync(
            new CheckOwnershipRequest
            {
                AssetId = assetId.ToString("D", CultureInfo.InvariantCulture),
                UserId = userId.ToString("D", CultureInfo.InvariantCulture),
            },
            cancellationToken: cancellationToken);
        return reply.IsOwner;
    }

    public async Task GrantConversationAccessAsync(
        Guid assetId,
        IReadOnlyList<Guid> userIds,
        string conversationId,
        Guid grantedByUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(userIds);
        if (userIds.Count == 0)
        {
            return;
        }

        var request = new GrantAssetAccessRequest
        {
            AssetId = assetId.ToString("D", CultureInfo.InvariantCulture),
            Source = GrantSource.Conversation,
            SourceId = conversationId,
            GrantedByUserId = grantedByUserId.ToString("D", CultureInfo.InvariantCulture),
        };
        request.UserIds.AddRange(userIds.Select(u => u.ToString("D", CultureInfo.InvariantCulture)));

        await grpcClient.GrantAssetAccessAsync(request, cancellationToken: cancellationToken);
    }
}
