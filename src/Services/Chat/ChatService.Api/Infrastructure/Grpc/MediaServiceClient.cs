using System.Globalization;
using MediaService.Api.Grpc;
using Urfu.Link.Services.Chat.Application.Messages;
using Urfu.Link.Services.Chat.Domain.Enums;

namespace Urfu.Link.Services.Chat.Infrastructure.Grpc;

internal sealed class MediaServiceClient(InternalApi.InternalApiClient grpcClient) : IMediaServiceClient
{
    public async Task<IReadOnlyList<MediaAssetMetadata>> BatchGetMetadataAsync(
        IReadOnlyList<Guid> assetIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assetIds);
        if (assetIds.Count == 0)
        {
            return Array.Empty<MediaAssetMetadata>();
        }

        var request = new BatchGetMetadataRequest();
        request.AssetIds.AddRange(assetIds.Select(a => a.ToString("D", CultureInfo.InvariantCulture)));

        var reply = await grpcClient.BatchGetMetadataAsync(request, cancellationToken: cancellationToken);

        return reply.Items
            .Select(item => new MediaAssetMetadata(
                AssetId: Guid.Parse(item.AssetId),
                OwnerId: Guid.Parse(item.OwnerId),
                Kind: (AttachmentType)item.Kind,
                SizeBytes: item.SizeBytes,
                MimeType: item.MimeType,
                OriginalFileName: item.OriginalFileName,
                IsUploaded: item.State == AssetState.Uploaded))
            .ToList();
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
